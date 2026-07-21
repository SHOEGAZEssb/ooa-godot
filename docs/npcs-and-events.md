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

The databases keep these three semantic outputs separate, but share
`NpcStoryState` for interaction keys, current-room-flag evaluation, and the
exact `getGameProgress_1`/`getGameProgress_2` state domains. Add a new shared
state selector there rather than copying progress logic between rule readers.

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

Room `2:ee` is the reference for talkable native handlers that hand control to
gameplay-owned modal ring menus and, separately, an unavailable serial link:

- Import all five positioned interactions in `mainData.s` order: Vasu and the
  two snakes are `$89:$00/$01/$06`; the Blue- and Red-Snake help books are
  `$e5:$00/$01`. Interaction `$e5` belongs in the ordinary visible-character
  extraction set even though its handler is a book rather than a person.
- State-0 visual overrides remain distinct. The snakes select their subids as
  animations `$01/$06`; both books retain default animation `$00`, while book
  subid `$01` increments only its OAM palette. Do not infer animation from the
  book subid. Import all `$89` talk/retreat animation parameters because state
  3 returns to idle on the first nonzero `animParameter`.
- `VasuShopNpcRoomEntity` owns only native per-update object behavior: Vasu's
  `$12/$06` collision and object-side Link separation, common drawing, and the
  snakes' strict Manhattan-distance `$18` emergence. Outside the radius the
  handler calls `interactionSetAnimation` every update, pinning the first
  hidden idle frame; inside it advances normally. Its talk target also uses
  `linkInteractWithAButtonSensitiveObjects` directly: the point is ten pixels
  ahead of Link and must lie strictly inside the actor's collision radii. This
  lets Vasu's `$12` vertical radius reach across his counter without widening
  global NPC talk bounds. Dialogue graph, choices, rewards, flags, and serial
  waits belong to `VasuShopEvent`.
- Resolve text `\call(TX_XXXX)` and `\jump(TX_XXXX)` control flow during import.
  A call returns and retains following text; a jump replaces the remaining
  source block. Preserve runtime `\stop`, `\col`, and `\opt` commands. This is
  required for TX `$3000 -> $303a`, `$300b/$300c -> $3016`, and the special-ring
  congratulation prefixes.
- The snake script-table predicate is exactly Ring Box obtained AND
  (`GLOBALFLAG_FINISHEDGAME` OR linked save). A finished or linked save without
  the Ring Box still uses the prelinked tutorial. Red Snake waits 30 original
  updates before the topic prompt. Blue Snake initializes its low counter to
  zero and high counter to two; absent serial interrupts, decrementing the
  resulting 16-bit counter shows TX `$300f` on update 512.
- Script-created Ring Box and ring interactions reuse `GroundTreasurePickup`
  in granted mode. Play the treasure behavior sound before grab-mode
  `SND_GETITEM`, use the two-hand pose for the box and one-hand pose for rings,
  and override the Ring treasure's `$ff` table parameter with the concrete
  ring ID as `giveRingToLink` does through `Interaction.var38`.
- Preserve Vasu's predicate/write timing. Special-ring received flags `$04-$06`
  are written before TX `$3036/$3037/$3039`; the linked first-time branch writes
  global `$08` and `wObtainedRingBox` bit `$01` only after its optional Ring Box
  reward. The ordinary path writes them only after the forced appraisal and
  list menus. `VasuShopEvent` must remain paused while `RingMenuController` owns
  the gameplay pause lease; resume its callback only after `closeMenu` has
  completed, then commit both fields so another same-room talk cannot duplicate
  the Friendship Ring.
- Appraisal is a two-phase inventory transaction. The selection first removes
  the source cost, increments `wNumRingsAppraised`, and clears the unidentified
  bit for the reveal. Only after the name and description close does it remove
  the queue entry and set the corresponding `wRingsObtained` bit; duplicates
  instead receive the source refund after the result wait. The first Vasu flow
  uses a zero cost, then opens the list menu before its script resumes.
- The list menu is the sole owner of Ring Box transfers. Keep a ring unique
  across its one/three/five available slots, remove it when selected in the
  same slot, and clear `wActiveRing` if the equipped ring is no longer in the
  box. A-button equip/unequip in the ordinary Inventory screen changes only
  `wActiveRing` and requires a populated slot.
- Unsupported serial boundaries remain explicit and safe. Actual Game Link
  transfer and linked secret input/generation emit source-addressed diagnostics
  without manufacturing a successful transfer or secret. Retail dialogue and
  the authentic no-cable branch before those boundaries remain implemented.

Lower Black Tower rooms `4:e0`, `4:e1`, `4:e2`, `4:e7`, and `4:e8` are the
reference for several native NPC handlers sharing one room slice:

- Import the five complete `mainData.s` streams, not only their visible NPC
  rows. The `$12:$00` dungeon handler in `4:e7` is invisible but occupies the
  second interaction slot, between soldier `$40:$0c` and the hardhat workers.
  Specialized adapters are emitted in that same order.
- Pickaxe worker `$57:$03` selects animation `$00` or `$01` from the exact
  eight-entry `var03` table. Unlike room `1:48`'s `$57:$00`, talking does not
  switch to a static pose: native animation continues, so a strike can still
  play `SND_CLINK` and create two `$92:$06` dirt chips while text is open. Each
  A-button talk consumes one shared `getRandomNumber` value and selects the
  eight-entry TX `$1b01`-`$1b05` table.
- Soldier `$40:$0c` likewise consumes one shared RNG value per talk for TX
  `$590d/$590e/$590f/$590d` and faces Link every update. Do not reuse the
  `GLOBALFLAG_FINISHEDGAME`/`GLOBALFLAG_0b` predicates from soldier subids
  `$00/$01`: the placed `$0c` dispatch jumps directly to `soldierSubid0c`, so
  both flags leave these construction soldiers alive.
- Patrolling hardhat `$58:$03` consumes its random TX `$100a/$100b/$100c`
  choice once during initialization, in object order, rather than per talk.
  Its imported `var03` patrol list stores raw direction/counter pairs. SPEED
  `$14` advances one half-pixel; the helper decrements before moving, does not
  move on counter zero, and waits 20 updates between legs. A-button polling is
  inside the movement helper, so that 20-update wait is not talkable. A talk
  retains its facing through the textbox plus the following 30-update
  input-disabled wait.
- Hardhat `$58:$00,var03=$00` reads current-room item bit `$20`. Clear shows TX
  `$1001`, waits 30 updates, executes `giveitem TREASURE_SHOVEL,$00`, waits 30,
  then shows TX `$1002`; set skips directly to TX `$1002`. The giveitem path
  grants the imported treasure, sets `$20`, plays the behavior and grab-mode
  `SND_GETITEM` calls, uses the exact interaction `$60:$1b` Shovel OAM, and
  holds it 14 pixels above Link in the two-hand pose until TX `$0025` closes.
  `var03=$01` always shows TX `$1000` and neither reads nor writes `$20`.
- Villager `$3a:$02` checks the open side at saved X plus or minus `$11` with
  temporary `$05/$03` radii. On strict Link collision it disables input, moves
  16 pixels at SPEED `$28`, restores Link's saved Y high byte, waits 10, saves
  the new X, reverses direction, and resumes watching. Both native substates
  call `interactionAnimateAsNpc`, so preserve its
  `objectPreventLinkFromPassing` separation before the open-side test or script
  movement. In particular, the final wait update separates Link before
  `enableinput`; otherwise Link can regain control while still overlapping the
  room blocker and cannot move away.
- Dungeon-stuff `$12:$00` retains its source slot on every `4:e7` parse, while
  a destination byte at or above `$f0` is represented as a distinct
  screen-warp entry context. On its first update it deletes unless that
  whiteout context is active and Link Y is at least `$78`; otherwise its strict
  radius `$08` contact at `$88,$78` shows aliased TX `$020f` ("The Black
  Tower"), records the death-respawn point, and deletes. Ordinary scrolling
  uses the same enemy edge-exclusion geometry but must not activate this
  whiteout-only interaction.

Global RNG calls in these handlers are part of the room contract. Room parsing
still consumes the shared 256-call placement-buffer shuffle first; patroller
initializers then consume values in source order, while soldier and pickaxe
talks consume later values only when A-button interaction reaches their helper.
Validations should compute expectations from that stream rather than reseeding
individual actors.

Rooms `4:08`, `4:09`, `4:0b`, `4:0c`, `4:22`, and `4:7a` are the references for
reusable invisible dungeon interactions whose shared state is
`wActiveTriggers` or the live enemy count:

- The importer emits all 155 direct `PART_BUTTON $09`, `$20:$00` permanent
  trigger-chest, `$21:$17` retractable trigger-chest, `$13:$01`
  push-block-trigger, and `$1e:$04-$0b` shutter placements: 49 buttons, 20
  trigger-controlled shutters, seven delayed chests, six retractable chests,
  and 73 live-enemy mechanism records. Each retains group, room, source object
  order, packed position, subid, parameter, trigger predicate, and whether a
  conditional enemy stream remains unresolved. Common button/shutter/chest
  tiles, collision radii, 28/30/15/8/6-update counters, and sound IDs are pinned
  separately.
- `PART_BUTTON` state 0 copies subid bits 0-2 as its `wActiveTriggers` index and
  returns before checking pressure. Bit 7 is behavior, not a save predicate:
  clear buttons latch tile `$0d` and their trigger for the rest of the room;
  set buttons return to `$0c` and clear the bit when released. Link contact uses
  strict `$02+$06` radii and requires ground Z. A block, pot, or other tile above
  a reusable button retains its visual while the underlying button becomes
  pressed, then starts the exact `$1c`-update release delay when it leaves.
  Both press and release request `SND_SPLASH` (`$87`). All 49 direct placements
  are unconditional; subid bits do not read global, room, linked-game, essence,
  or treasure flags.
- `$1e:$04-$07` select one trigger bit from their object parameter. An inactive
  bit closes the directional shutter; an active bit requests
  `SND_SOLVEPUZZLE` and opens it on the next update. These controllers remain
  alive after the common six-update interleaved animation so reusable buttons
  can close and reopen them. Room `4:09` proves one one-shot bit-0 button opens
  both its up and right shutters; room `4:22` proves reusable press/release.
  Once Link clears an entry shutter, its controller moves a door-tile local
  respawn one tile inward. If a reusable shutter later finishes closing on
  Link's tile, parameter-2 respawn hides him for two updates, returns him to
  that local point, applies one heart of damage, and holds the 16-update
  recovery state instead of leaving him embedded in collision.
- `$20:$00` uses its imported dungeon script to choose either exact-byte or
  bit-set trigger semantics. Room `4:08` requires `wActiveTriggers == $01`;
  the qualifying update requests `SND_SOLVEPUZZLE`, creates `INTERAC_PUFF`
  with `SND_POOF`, waits exactly 15 updates, then writes chest tile `$f1`.
  Once `ROOMFLAG_ITEM` is set, re-entry writes opened chest tile `$f0` at the
  imported position and retires the controller. `$21:$17` also compares the
  complete trigger byte: it writes `$f1` and puffs immediately when active,
  then restores the saved source-layout tile and puffs again when inactive.
  Only appearance requests the solve cue. Room `4:7a` exercises both edges.
- `$13:$01` occupies the first `4:0c` slot. State 0 saves the source tile,
  writes logical pushable tile `$1d` without redrawing, and contributes one to
  `wNumEnemies`. Once only that sentinel remains it restores the source tile;
  after the push changes the source layout, it waits exactly 30 updates and
  removes its enemy-count contribution before the later door slot updates.
- `$1e:$08-$0b` query the shared live count. A count that begins at zero opens
  without the solve cue; a transition from nonzero to zero requests
  `SND_SOLVEPUZZLE`, waits eight updates, and begins opening on the following
  update. Enemy object flag `$02` remains count-exempt, as exercised by room
  `5:93`'s six living Keese. Direction selects closed tiles `$78-$7b` and
  interleave type 0-3.
- During an ordinary scroll, `replaceShutterForLinkEntering` compares the
  transition direction and Link's destination packed position against the
  room's directional shutters. Only the shutter Link actually crosses is
  preloaded as non-solid floor `$a0`; other shutters retain their closed source
  tiles. After destination objects unfreeze, the controller waits while Link
  overlaps the original directional `$08/$0a` radii plus Link's `$06` radius.
  It begins closing on the update after Link clears that strict boundary, keeps
  floor collision throughout the six-update interleaved frame, and installs
  the solid directional tile only when that frame completes.
- Door movement requests `SND_DOORCLOSE` only inside the original screen
  boundary, renders the source mapping's directional half over open tile `$a0`,
  retains the old collision for six updates, then writes the full destination
  tile, updates collision, and repeats the visible sound. Enemy-door controllers
  delete after opening; trigger-controlled variants return to their bit loop.
- `RoomEntityFactory` always enables imported buttons and trigger-controlled
  shutters. It enables `$13:$01` and enemy shutters only where the active,
  counted enemy stream is completely represented. An unsupported counted enemy
  type or unresolved before/after-event stream preserves those count-dependent
  mechanics at the source layout until a truthful `wNumEnemies` count is
  possible. Room `4:0b` is the enemy-count regression: its sole enemy row is
  always active
  (`obj_RandomEnemy $60 $43 $00`, count 3, no low flag bits), so it has no
  global, room, linked-game, essence, or treasure predicate. Three ordinary
  Gels gate simultaneous up and left shutters through the same entity contract.
- Killable enemy objects without source flag `$01` receive the original
  one-based indices `$01-$07`. `RecentEnemyDefeats` mirrors
  `wEnemiesKilledList`: eight room IDs and their killed-index bitsets in a ring,
  retained across scrolling and dungeon warps but cleared by non-dungeon warp
  loading. A cached mutable room restores each shutter to either its closed
  source tile or the entry-only `$a0` substitution on every parse. A short
  `4:0b` re-entry suppresses all three Gels and runs the zero-count door branch
  without `SND_SOLVEPUZZLE`; an uncleared left entry delays tile `$7b` until
  Link is fully inside. Red Zol split Gels inherit their parent's index,
  matching the original `Enemy.enabled` transfer.

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

Room `1:76` is the corresponding reference for an invisible placed interaction
that is neither an NPC nor a tile-table warp:

- Its only object is `INTERAC_MISCELLANEOUS_2` `$dc:$10` at `$42,$50`. The
  cutscene importer emits that placement together with the handler's layout
  writes, collision radii, flag mask, hardcoded destinations, raw transition
  bytes, and sound. A controller must not invent a talkable actor merely
  because the room was selected during an NPC implementation pass.
- State 0 changes packed layout positions `$44/$45` to metatile `$00`. These
  are transient `wRoomLayout` writes, not persistent room-specific tile-change
  rules; every room parse restores its selected base layout before the event
  clears the doorway again. The handler issues no BG redraw, so those logical
  clears affect collision while the already-rendered doorway remains unchanged;
  the invisible interaction draws no overlay of its own.
- The placed object's `$04/$10` Y/X radii combine with Link's imported
  `$06/$06` radii using the original strict comparison. State 0 advances
  directly to armed state 2 when Link starts outside. When Link starts inside,
  it enters state 1 and must observe one non-colliding update before a later
  re-entry can warp, preventing the return entrance from immediately looping.
- The sole predicate is current room `1:76` bit `$01`: clear selects room
  `4:e7`, set selects `4:f3`. This is `ROOMFLAG_LAYOUTSWAP`, so the same bit also
  selects group 1 versus group 3 base room data through `RoomSession`; it is not
  a global, linked-game, essence, or treasure predicate.
- Raw hardcoded values `$93/$ff/$01` mean destination transition 3, entering
  upward from the middle bottom, with immediate room loading. The interaction
  itself plays `SND_ENTERCAVE` `$6e`. `BlackTowerDoorwayEvent` remains a
  non-blocking room event while armed, so ordinary gameplay and menus continue.

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

Room `1:38` is the reference for an event whose interaction scripts create
more script owners and later hand combat back to the entity system. Keep the
sprout, `$6b:$04` controller, left `$96:$00` Moblin, right `$96:$01` Moblin,
and `$76:$01` gate opener in original update order. Their runners share only
the imported `wTmpcfc0` state, `wccd4` synchronization byte, and live
`wNumEnemies` view. The Moblin lanes replace their own actors with normal
masked-Moblin enemies; the event must not retain a parallel kill counter.
The sprout's state-0 handler immediately runs its script, and the newly created
controller and Moblins occupy later interaction slots that initialize in the
same object pass. Prime that one pass synchronously when the room entities are
loaded: a warp must already contain the distressed sprout and both Moblins
under its fade, and a scrolling destination must contain them at its first
visible edge. Stop at the controller's installed 60-update wait; subsequent
event time remains frozen with ordinary destination events during transition.
The original room-loading loop advances the sprout's following animation `$02`
command beneath the warp fade. Because the runtime event clock freezes during
the host transition, stage that imported visual without consuming the command
runner or decrementing the controller wait; otherwise the ordinary happy pose
flashes as the fade clears. Read this pose from the command's encoded animation
payload, not from the sprout actor's ordinary directional animation fields;
those fields all describe animation `$00` and therefore reproduce the flash.
Likewise, `wDisableScreenTransitions` blocks only edge scrolling while Link is
playable during the approach, fight, and final edge wait. The four gate phases
own their mapping-interleaved tiles, collision completion, four-puff bursts,
two-axis RNG shake, and persistent room bit `$80`.
The post-fight Link path must retain its apparently redundant final Y=`$38`
waypoint: `linkCutscene_updateAngleOnPath` uses it to select DIR_UP before
restoring ordinary Link. TX `$05d4` separately carries `\pos(2)` and therefore
opens at the bottom even though the preceding edge test accepts any room edge.
The completion layout bit for past room `1:48` exposes tile `$d7` beneath its
placed `$e1:$02` time-portal spawner. `timeportalSpawner.s` sets that subtype's
active bit while `TREASURE_SEED_SATCHEL` is absent, so leaving the rescue room
through the bottom must preload an already-active portal at `$48/$58`; do not
drop subtype `$02` merely because later, Satchel-owning visits require the Tune
of Echoes.

Present room `0:38` is the reference for a talk-triggered event that keeps an
infinite original NPC script active without blocking ordinary gameplay:

- `wMakuTreeState=$02` selects `makuTree_subid02Script_body`; place this event
  ahead of the earlier disappearance handler in room-event priority. `HasState`
  remains true for the script's NPC loop, while `BlocksGameplay` follows only
  `disableinput`/`enableinput`.
- `makeabuttonsensitive` arms the placed `$87:$00` actor and `checkabutton`
  consumes its routed talk press. This actor has no ordinary NPC text ID, so
  target acquisition must honor its script-sensitive state before applying the
  usual nonzero-text requirement. The initial room-bit `$80` branch skips the
  first conversation on re-entry. TX `$054a` is a real choice: option `$00`
  returns to `@explainAgain`; option `$01` continues.
- Keep `makuTree_dropSeedSatchel` and `makuTree_checkSpawnSeedSatchel` as native
  commands at their source boundaries. The drop chooses X `$50` outside
  Link-X `$3c-$63`, X `$60` for `$3c-$4f`, and X `$40` for `$50-$63`; it stores
  that byte at `$c6eb` and sets room bit `$80` before creating the treasure.
- The created `INTERAC_TREASURE $60` is still the reusable ground-treasure
  entity. Var03 `$02` means spawn mode `$02`, grab mode `$01`: wait 40 updates,
  begin immediately above the screen, integrate Z with gravity `$10`, play
  `SND_DROPESSENCE` on two landings, and bounce once at speed `-$00aa`.
  Var03 `$03` respawns instantly at Y `$58`. Collection uses Link's one-hand
  pose at offset `(-4,-14)` and sets room item bit `$20`, which suppresses all
  later respawns.

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
- `src/interactions/BlackTowerWorkerDatabase.cs`
- `src/interactions/BlackTowerWorkerInteractions.cs`
- `src/interactions/InteractionController.cs`
- `src/interactions/GroundTreasureDatabase.cs`
- `src/interactions/GroundTreasurePickup.cs`
- `src/cutscenes/RoomEventController.cs`
- `src/cutscenes/RoomEventContext.cs`
- `src/cutscenes/PreBlackTowerEvent.cs`
- `src/cutscenes/PreBlackTowerEventDatabase.cs`
- `src/cutscenes/BlackTowerDoorwayEvent.cs`
- `src/cutscenes/BlackTowerDoorwayEventDatabase.cs`
- `src/cutscenes/BlackTowerEntranceEvent.cs`
- `src/cutscenes/BlackTowerEntranceEventDatabase.cs`
- `src/cutscenes/BlackTowerExplanationScreen.cs`
- `src/cutscenes/MakuSproutRescueEvent.cs`
- `src/cutscenes/MakuSproutRescueDatabase.cs`
- `validation/GameRoot.Validation.cs`

See [Rooms and entities](rooms-and-entities.md) for room lifetime and capability
contracts, [Saves and runtime state](saves-and-state.md) for flag ownership, and
[Validation](validation.md) for the regression assembly boundary.
