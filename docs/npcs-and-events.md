# NPC and room-event implementation

This guide records the current method for porting NPC interactions and room
events. In the disassembly, a visible character is usually an interaction
object whose initialization, script, native handler, and related parts can have
different owners. Start from those original owners; do not choose an
architecture from the character's appearance or from the size of the scene.

The common pipeline is:

```text
ROM behavior and disassembly
        -> strict importer records with source identity
        -> typed runtime database
        -> room entity, linked interaction, or room event
        -> separate headless regression
```

Generated records carry data across the disassembly/runtime boundary. Runtime
code never parses assembly files, and production classes do not contain
validation-only traces.

## Choose the original owner

Use the smallest owner that preserves the original mechanism and update order:

| Original behavior | Runtime representation |
| --- | --- |
| Positioned character with ordinary animation, solidity, facing, and text | Imported `NpcRecord`, `NpcCharacter`, and `NpcRoomEntity` |
| State-0 branch deletes or retains a placed interaction | Imported `NpcVisibilityRuleDatabase` predicate |
| The actor remains but selects different text from story state | Imported `NpcDialogueRuleDatabase` rule |
| Initialization selects a different static position from story state | Imported `NpcPositionRuleDatabase` rule |
| Native object code advances movement, counters, animation, or collision every update | Specialized `IRoomEntity` adapter implementing the required capabilities |
| Several interaction slots exchange signals or own a shared part | One linked interaction state owner, with one adapter/update call per original object slot |
| Room entry starts a sequence that coordinates Link, dialogue, transitions, flags, audio, or several actors | Dedicated event owned by `RoomEventController` |
| The original behavior is an `interactionRunScript` stream | Typed `CutsceneCommandRunner` inside the owning entity or event |
| The original is a native cutscene, transition, palette thread, or special-object state machine | Specialized native controller, with typed script lanes only for script-driven portions |

Complexity alone is not a reason to promote an NPC into a room event. A moving
villager that remains an ordinary room object belongs in `RoomEntityManager`.
Conversely, a short sequence that changes input, rooms, or persistent story
state should not be hidden inside `NpcCharacter` merely because one NPC starts
it.

## Build an evidence packet first

Trace all inputs that can affect the interaction before editing runtime code:

1. Find its room-object placement and preserve the surrounding object order.
2. Follow the interaction ID/subid dispatch into state 0 and later states.
3. Trace every called script, helper routine, native handler, created
   interaction/part, and shared temporary byte.
4. Record flag addresses, masks, aliases, branch precedence, and whether a
   branch deletes the object or changes its behavior in place.
5. Record initialization order, per-object update order, counter installation,
   decrement boundaries, and same-update versus next-update work.
6. Trace positions, collision radii, speed/angle arithmetic, fixed-point Z,
   animation indices/frames, OAM, palettes, text IDs, textbox position, sounds,
   and input-disable masks.
7. Trace completion writes, room exit, scrolling, cancellation, re-entry, and
   save/reload behavior.

Executed clean-ROM behavior has priority when it can be observed. The
disassembly remains the primary implementation trace and should be searched for
callers and data tables, not just for a promising routine name.

Keep original identifiers in importer diagnostics, runtime assertions, source
comments, and validation failures. A message such as `Room 1:49 is missing
interaction $3c:$0e` is actionable; a message such as `missing boy` is not.

## Importer boundary

`tools/import_oracles/Import-NpcData.ps1` owns ordinary NPC placement, graphics,
animation inputs, text, visibility predicates, dialogue selection, and small
interaction-specific data sets. `Import-CutsceneData.ps1` owns typed command
streams and event records that span several systems.

Prefer one of these generated forms:

- A base NPC row for placement, `var03`, initial visual, directional
  animations, text, and facing behavior.
- A visibility row for a state-0 deletion predicate.
- A dialogue row when the same actor selects text from live story state.
- A position row when initialization chooses a static coordinate from story
  state and the actor otherwise keeps its object-data position.
- A family/spawner row when an original table selects one of several actor
  records by stage or personality.
- A narrowly typed visual, physics, timing, or event record for native behavior
  not represented by the common NPC schema.
- A source-addressed command stream for original interaction scripts.

Do not encode dialogue strings, sprite choices, or parsed script operands in a
runtime controller when they can be emitted by the importer. Do not invent a
generic format that erases an original distinction merely to avoid a small
typed record.

Importer checks must fail if a consumed handler or table no longer matches the
expected source structure. For a specialized interaction, assert every branch
input that runtime code depends on, not only the visually interesting branch.
If a reusable generated predicate schema does not yet express a native state
machine, exact masks may remain in that specialized runtime owner only when the
importer pins the source sequence and validation covers its complete truth
table. Extend the general schema when the same mechanism appears again.

Generated output under `assets/oracle/` is never edited directly. Regenerate it
after importer changes and keep row order deterministic.

## Ordinary data-driven NPCs

The ordinary path is intentionally small:

1. `NpcDatabase` loads the generated room rows in source order.
2. `RoomEntityFactory` creates one `NpcCharacter` and adapter for each selected
   row.
3. `RoomEntityManager` adds the adapters to the active entity list in that
   order.
4. `NpcCharacter` owns rendering, common animation, Link awareness, facing,
   collision radii, talk range, and resolved dialogue presentation.
5. `InteractionController` asks the entity manager for the first `ITalkTarget`,
   applies facing, and opens the imported text at the actor's textbox position.

Visibility, dialogue, and state-selected position are separate because they
reproduce different original effects:

- Visibility rules model initialization branches that delete an interaction.
  Rules within one alternative are ANDed; alternatives are ORed. A `var03`
  selector can distinguish placements sharing one ID/subid.
- Dialogue rules model script selection while the interaction remains alive.
  Exactly one applicable rule may resolve for an actor. A linked-game selector
  distinguishes branches within the same story-state table entry without
  inventing another progress state.
- Position rules model an initialization branch that replaces object-data
  coordinates without introducing per-update motion. A living actor without a
  matching state uses its original room-object coordinates.

`RoomEntityManager` reevaluates these rules when `OracleSaveData` or
`OracleRuntimeState` changes. Ordinary visibility uses `SetFlagVisible` rather
than destroying the Godot node, allowing mutually exclusive imported variants
to swap live without reparsing room data. Event-owned deactivation remains a
separate active-state decision. Ordinary position refresh likewise resolves
from the original object-data coordinates every time, so leaving an override
state restores the source position instead of retaining stale coordinates.

Use the original state domain. Current visibility inputs include global,
current-room, specific-room, treasure, linked-game, essence, save-WRAM,
transient runtime-WRAM, and `getGameProgress_1`/`getGameProgress_2` conditions.
New kinds require a typed evaluator and strict importer validation; do not fold
an unknown state function into a nearby flag that happens to match one save
file.

## Native and linked room interactions

When an ordinary interaction has native per-update behavior, add only the room
entity capabilities it actually uses. Exact original-engine counters and
movement belong in `IFixedRoomEntity.UpdateFrame`; delta-driven presentation
belongs in `IVariableRoomEntity` only when it is intentionally variable.
Solidity and talking remain independent through `IRoomBlocker` and
`ITalkTarget`.

For linked interactions, one class may own shared state, but each original
object slot still receives its update in room-object order. Expose a separate
adapter or update delegate per actor instead of advancing the whole group from
the first actor. This preserves temporary-byte handshakes and cases where a
created part occupies a later slot.

Room `1:49` is the current reference pattern:

- `Room149FamilyInteraction` owns the father/son signal, ball ownership, and
  three-state tableau.
- Separate boy, father, observer, and ball adapters are yielded in original
  update order.
- Generated visual/text records retain the selected animation and dialogue
  data.
- The exact D7/Veran predicate changes behavior in place; it is not a visibility
  rule because all three characters remain instantiated.
- `IRoomSaveStateEntity` refreshes the tableau immediately when live save state
  changes.

`RunningBipinRoomEntity` is the smaller reference: it adds the original
fixed-update patrol and reversal behavior while continuing to use
`NpcCharacter` for rendering, talking, and collision.

A group/room branch in `RoomEntityFactory` is acceptable only when the original
defines that exact linked composition. Prefer interaction ID/subid dispatch for
behavior shared across placements, and import a general selector table when the
source has one. Do not grow a list of observed-room exceptions.

Spawned interactions, parts, and effects use `RoomEntitySpawn`. Preserve
whether a child updates on its creation frame through `UpdateThisFrame`; that
boundary must come from the original creation/update loop.

Room `1:48` is the reference for a single native NPC with ordinary neighbors:

- The male villager and past girl remain generic NPCs; imported
  `getGameProgress_2` visibility and dialogue rules select their exact living
  states and linked-game text branches.
- `Room148PickaxeWorkerRoomEntity` advances animation `$02` once per original
  update. Imported animation parameters `$01`/`$02` are read after that advance
  and directly trigger SND `$50` plus two dirt-chip spawns.
- `INpcTalkLifecycle` mirrors the worker script's animation `$03` while TX
  `$1b00` is open and its same-update return to animation `$02` when text
  closes. Use this lifecycle only when the original interaction script changes
  persistent per-update behavior around a textbox.
- Each `Room148PickaxeDebris` preserves the `$92:$06` state-0 return,
  palette-from-parent parameter, source-order angles `$18`/`$08`, SPEED `$14`,
  and `-$00c0`/`$18` fixed-point Z flight. Its graphics index `$00` loads no
  dynamic object sheet, while interaction flag `$81` selects fixed VRAM bank 1;
  therefore tile base `$02` comes from `spr_common_sprites`, not the worker
  sheet. Preserve both the no-load index and bank bit when resolving such child
  effects to a source PNG.

Animation records retain a frame's nonzero `animParameter` as
`duration,parameter@oam`; frames with parameter zero keep the compact
`duration@oam` form. A native owner must inspect the parameter at the same point
relative to `interactionAnimate` as the original handler.

Room `1:57` is the minimal ordinary-predicate reference. Female villager
`$3b:$05` imports its `getGameProgress_2` existence set and complete eight-entry
dialogue table. Its initializer's final `oamFlags = $01` palette replaces the
graphics-header default in the base NPC row; a constant visual override does
not require a specialized runtime owner.

Room `1:58` is the reference for story-selected ordinary NPC state without a
specialized runtime owner:

- The hobo `$44:$04` imports all eight `getGameProgress_2` script choices,
  including the linked/unlinked text branches in states `$01` and `$07`.
- His state-0 deletion is a visibility rule for state `$03`; his `$58,$78`
  coordinate in state `$06` is a position rule; every other living state falls
  back to the room-object coordinate `$48,$48`.
- Impa `$4f:$02` imports the exact `getImpaNpcState == $08` conjunction,
  including present-room `$83` bit `$80`, both treasures, and all global flags.
  Nayru `$36:$0d` imports Flame of Despair set and finished-game clear as one
  alternative. Their fixed TX IDs and directional facing remain base NPC data.
- These records stay ordinary `NpcRoomEntity` adapters because none of the
  three handlers owns native movement, shared signals, or a per-update state
  machine after initialization.

Room `1:75` demonstrates the boundary between ordinary predicates and a
coordinated event in one object stream:

- Hardhat worker `$58:$01` remains an ordinary NPC. `var03=$00` requires both
  present-room `$90` and `$ba` bit `$40` clear; `var03=$01` requires `$ba` set
  and `$90` clear. This preserves `getBlackTowerProgress` checking `$90` first,
  so progress `$02` deletes both placements. Their TX `$1007`/`$1008` choices
  are variant-specific base records rather than event dialogue.
- Ralph `$37:$0a`, Impa `$31:$04/$05`, Nayru `$36:$0a`, and Zelda `$ad:$04`
  retain their ordinary placement and state-0 visibility predicates. The event
  resolves those actors instead of creating duplicate scene copies.
- `PreBlackTowerEvent` owns the input-disabled choreography because the actors
  exchange `$cfc0` bits or the shared `$cfd0` byte, force Link movement, spawn
  Nayru `$36:$09` and exclamation interactions, and write global completion
  flags. Each original interaction script is imported as an independent typed
  lane and advanced in room-object order.
- Initializer writes that precede a script remain explicit runtime inputs.
  Ralph's unlinked lane begins with native `SPEED_180` (`$3c`); Zelda's linked
  lane begins with `SPEED_100` (`$28`) and angle `$08` before its first
  `applyspeed`. Seed runner motion registers at lane start instead of inserting
  synthetic script commands that would change yield timing.
- The unlinked sequence deliberately releases gameplay after Ralph leaves and
  keeps Impa armed until Link reaches the original Y threshold. Event state and
  gameplay blocking are separate so re-entry after
  `GLOBALFLAG_RALPH_ENTERED_BLACK_TOWER` resumes the pending Impa phase without
  replaying Ralph.

Room `1:86` demonstrates an A-button script that hands off to a native
presentation and resumes through a same-room warp:

- Hardhat worker `$58:$02` is still the placed, solid, talkable actor. Essence
  bit `$08` deletes it in state 0. Room bit `$80` selects TX `$1004` and moves
  its initialized position from `$38,$48` to `$38,$58`; these are ordinary
  visibility/dialogue/position predicates rather than event-only copies.
- The first and aftermath portions of `hardhatWorkerSubid02Script` are separate
  imported typed lanes. The first talk shows TX `$1003`, waits 30 updates,
  writes room bit `$40`, saves Link's packed position/direction, and triggers
  stage 0 of `CUTSCENE_BLACK_TOWER_EXPLANATION`.
- `BlackTowerEntranceEvent` natively owns the original full-screen graphics
  headers, palettes, OAM `$714c`, white fade, shared-RNG lightning, TX `$1005`,
  and medium music fade. Its temporary 160-by-144 fade draws above the HUD so
  the gameplay and status-bar regions share one uniform white fade; ordinary
  room transitions regain their 160-by-128, below-HUD fade afterward. The
  source cutscene returns through destination transition `$0c`; reparsing room
  `$86` observes bit `$40` and starts the aftermath lane on the newly created
  guard instead of retaining a stale node.
- The aftermath preserves its 60/30/30 waits, Link-away simulated input,
  `SPEED_080` raw speed `$14`, `moveright $21` counter boundary, final room bit
  `$80`, and release of input. Completed A-button talks then use the ordinary
  TX `$1004` loop.

Interaction `$dc:$07` in the same room is not part of the guard event. It is a
general ground-treasure spawner used in eight rooms:

- The importer emits its original object order, position, Heart Piece treasure
  object, graphics, palette, and static OAM animation.
- `RoomEntityFactory` creates it only while room flag `$20` is clear. Its fixed
  state-0/state-1 exposure and strict combined `$0c` Link collision run through
  `IFixedRoomEntity` and `ILinkContactEntity`.
- Collection gives the imported treasure and sets `$20` immediately; on the
  following update it raises the item 14 pixels above Link, selects
  `LINK_ANIM_MODE_GETITEM2HAND`, and plays the second `SND_GETITEM`. The entity
  and pose remain until TX `$0017` closes. The inline `\heartpiece` control
  draws the source four-tile diagram at the text cursor, initially shows one
  fewer piece, blocks for 30 updates, fills the newly collected quarter, and
  plays `SND_TEXT_2`. For the fourth piece that update also clears the piece
  counter; the following A/B press plays `SND_FILLED_HEART_CONTAINER`, grants
  and refills a four-quarter Heart Container, and replaces the remaining text
  with TX `$0049` before the pose is released.

Keep independent interactions independent even when they share a room. Room
`1:86` validation therefore exercises the guard flags `$40/$80`, deletion bit
`$08`, and treasure bit `$20` separately before running the combined room flow.

## State predicates and live changes

Keep four questions distinct:

| Question | Typical owner |
| --- | --- |
| Should this placed interaction exist? | Visibility predicate |
| Which text should the living actor use? | Dialogue predicate |
| Which static position should initialization select? | Position predicate |
| Which native behavior, palette, or moving state is active? | Specialized interaction state machine |
| Has a one-shot event completed on re-entry? | Original save or room flag read by the event |

Preserve branch order rather than reducing it to unordered booleans. For
example, room `1:49` checks room `4:fc` bit `$80` before essence byte `$c6bf`
bit `$40`; the first branch therefore wins when both are set. Validation covers
all four combinations, not only the two states normally seen in sequence.

Also validate negative space: unrelated bits in the same byte, the same mask in
another room or group, room-table aliases, and clearing a flag live. A broad
mask or wrong aliased table can otherwise pass the happy path.

Persistent predicates read `OracleSaveData`; transient original WRAM reads
`OracleRuntimeState`. Do not cache either in a clone-only completion boolean.
Use `IRoomSaveStateEntity` when a living specialized entity must react
immediately. When the original only selects actors during room parsing, perform
selection at load and validate exit/re-entry instead of adding invented live
replacement behavior.

## Room events and cutscenes

`RoomEventController` owns room-entry and story sequences that coordinate more
than ordinary entity behavior. It keeps an explicit priority list and advances
one active primary event at 60 original updates per second.

An event with ordinary room-match/start behavior implements `IRoomEntryEvent`:

- `Matches` checks imported room identity and only the state needed to select
  that entry path.
- `Start` resolves required placed actors through `RoomEventContext.RequireNpc`
  using group, room, interaction ID, and subid.
- `HasState` reports real active work; `BlocksGameplay` derives from the
  original input/object-disable behavior.
- `UpdateFrame` advances one original update.
- `Cancel` clears runners, actor references, temporary palettes/effects, and
  other event-owned transient state.

Cross-room sequences such as following Impa or the Nayru introduction retain
specialized coordination in `RoomEventController`; forcing them through a
simple room-entry interface would lose actor transfer and cancellation order.

Placed characters remain `RoomEntityManager` entities when possible. Events
operate on those actors rather than creating parallel scene-owned copies.
Actors genuinely created by scripts use typed `RoomEntitySpawn` requests.
`RoomEventContext` is the narrow bridge to room state, entities, transitions,
Link, dialogue, presentation, inventory, and sound; event classes should not
reconstruct those owners.

Use the [Cutscene command runner](command-runner.md) when the original advances
an interaction script. The event remains the host and validates its supported
actors and operations. Native palette, transition, follower, physics, or effect
handlers remain narrowly named native operations at their original command
boundary. Do not turn a native state machine into an approximate command list
because both contain waits.

Ordinary destination entities and events stay frozen during scrolling. Add a
transition update path only for an original exception, such as a retained
follower or the first-past arrival overlap. Completion must be represented by
the original global/room/WRAM write so room re-entry naturally selects the
correct state.

## Validation pattern

NPC and event regressions live in `validation/GameRoot.Validation.cs`, not in
production classes. Build a canonical-room scenario through the same public or
internal lifecycle used by gameplay.

For an ordinary NPC slice, cover as applicable:

- imported record count/order and exact ID/subid/`var03` selection;
- initial position, animation, palette, collision, talkability, and text;
- every visibility/dialogue alternative and live state refresh;
- unrelated masks, room/group aliases, and mutually exclusive variants;
- movement or animation update boundaries and room-transition freeze;
- exit/re-entry after persistent state changes.

For a linked interaction or event, additionally cover:

- original per-actor update order and shared-signal boundaries;
- exact waits, counter installation/decrement behavior, movement, Z physics,
  sounds, dialogue, and spawned-object timing;
- successful and rejected entry predicates;
- input/gameplay blocking, cancellation, transitions, and retained actors;
- completion flags plus completed re-entry behavior;
- every supported script branch and native-handler handoff.

Attach command traces from the validation assembly when command boundaries are
observable. Trace script label, command index, source line, opcode, start,
update, and completion update, plus the branch result. A trace observes
production behavior; it must not drive it.

## Adding an NPC or event slice

1. Trace placement, dispatch, callers, scripts, native handlers, graphics, and
   every state branch.
2. Write down the original owner and select the matching runtime path from the
   ownership table above.
3. Extend `Import-NpcData.ps1` or `Import-CutsceneData.ps1` with strict,
   source-aware parsing and the smallest typed records.
4. Regenerate assets; never repair generated rows manually.
5. Add the ordinary adapter, specialized linked owner, or event without moving
   unrelated mechanics into it.
6. Preserve object order, fixed-update boundaries, arithmetic, collision,
   spawn timing, and transition behavior.
7. Route flags and WRAM through `OracleSaveData` or `OracleRuntimeState`, with
   exact masks, aliases, and precedence.
8. Add a canonical headless regression for every supported branch, negative
   predicate cases, live/re-entry behavior, and exact timing.
9. Update [Implementation status](implementation-status.md) for new
   player-visible coverage and [TODO.md](../TODO.md) for deliberate remaining
   work.
10. Run the importer when changed, build with zero warnings/errors, run the
    complete headless suite, and finish with `git diff --check` and
    `git status --short`.

## Relevant files

- `tools/import_oracles/Import-NpcData.ps1`
- `tools/import_oracles/Import-CutsceneData.ps1`
- `src/entities/NpcDatabase.cs`
- `src/entities/NpcCharacter.cs`
- `src/entities/NpcVisibilityRuleDatabase.cs`
- `src/entities/NpcDialogueRuleDatabase.cs`
- `src/entities/NpcPositionRuleDatabase.cs`
- `src/entities/RoomEntityFactory.cs`
- `src/entities/RoomEntityManager.cs`
- `src/entities/RoomEntityContracts.cs`
- `src/entities/RoomEntityAdapters.cs`
- `src/interactions/Room148PickaxeDatabase.cs`
- `src/interactions/Room148PickaxeInteraction.cs`
- `src/interactions/InteractionController.cs`
- `src/interactions/GroundTreasureDatabase.cs`
- `src/interactions/GroundTreasurePickup.cs`
- `src/cutscenes/RoomEventController.cs`
- `src/cutscenes/RoomEventContext.cs`
- `src/cutscenes/PreBlackTowerEvent.cs`
- `src/cutscenes/PreBlackTowerEventDatabase.cs`
- `src/cutscenes/BlackTowerEntranceEvent.cs`
- `src/cutscenes/BlackTowerEntranceEventDatabase.cs`
- `src/cutscenes/BlackTowerExplanationScreen.cs`
- `validation/GameRoot.Validation.cs`

See [Rooms and entities](rooms-and-entities.md) for room lifetime and capability
contracts, [Saves and runtime state](saves-and-state.md) for flag ownership, and
[Validation](validation.md) for the regression assembly boundary.
