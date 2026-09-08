# Menus and input

## Modal ownership

Only one modal client may own gameplay input and the shared menu presentation at
a time. `OracleMenuLifecycle` owns the common map/inventory lifecycle:

```text
Closed -> OpeningFadeOut -> OpeningFadeIn -> Open
Open   -> ClosingFadeOut -> ClosingFadeIn -> Closed
```

The common fast fade lasts 11 original updates in each direction. The screen
swap occurs at full white. A menu-to-menu switch may retain ownership while
white; it must not briefly resume gameplay between screens. Timing-critical
fades use the fixed-update controller, not a generic tween.
The white fade adds and saturates integer 5-bit color-channel offsets using
the source speed and an additive scene material; it does not interpolate RGB.

`GameplayPauseController` provides an exclusive, owner-checked lease. It saves
the exact processing/input state it suspends and restores that state on
release. Never blindly enable Link when a menu closes: another owner may have
disabled gameplay first. Failed acquisition leaves the caller unchanged.

Gameplay-owned submenus may use their interaction controller when that matches
the original mechanism. Do not force every prompt through the map/inventory
lifecycle; preserve its update masks, fade, and screen boundary.

Short-secret entry uses the application-owned shared pause/fade lifecycle.
Its keyboard, glyph mapping, lower cursor offsets and error overlay are imported
from the source; the controller retains source button priority and repeat timing.
Validation and return-secret generation share the linked-game codec and live
save game ID. An interaction receives its result only after the closing fade
releases menu ownership, so its script waits do not run behind the menu.

## Screen-space boundaries

The Gale Seed menu shares the map presentation and menu lifecycle. It selects
only visited destinations from the imported ordered tree table, including the
present scent tree's planting flag. Confirming a destination transfers at white
to the room transition owner; canceling restores Link's falling state in the
existing room. Neither path uses development fast travel.

Full-screen menus and their fade use 160 by 144 screen space, including the
HUD. A room-warp fade covers only the gameplay field at y=16-143. Ordinary room
dialogue starts with field-relative positions and adds the 16-pixel display
offset; pregame and full-screen presentations do not.

Textbox placement follows `initTextbox`'s unsigned byte subtraction of Link Y
and camera Y, then `initTextboxStuff`'s rounded tilemap address and hardware
SCY. Native events expose their source screen context through
`IRoomEventDialogueContext`; the event stage remains its authoritative owner.
A cutscene that clears WRAM bank 1 supplies cleared Link coordinates to that
context without moving the retained gameplay player. Imported graphics register
states distinguish the palace HUD layout from the full-screen Black Tower.
Returning or cancelling the event restores normal player/camera selection.
Explicit position operands are reserved for original text/script commands.

Imported presentation records own source-ordered tilemaps, OAM, cursor
locations, palette selections, and layout data. Menu controllers own input,
cursor transitions, state changes, modal phases, and update timing. Apply Game
Boy OAM offsets, signed byte wrap, and hardware coordinate biases at the
rendering boundary instead of baking corrected coordinates into imported data.

Map mode follows the effective tileset flags, including the imported indoor-era
bit table. Room entry maintains the saved minimap position and dungeon visited
floor mask; side-view rooms retain the preceding top-down cell. Floor visibility
reads that mask independently of individual room visit flags. Dungeon scrolling
copies an 18-row window through source-ordered floors and their blank separators,
moving one tile per update and consuming a final zero-counter update. Sprite
overlap follows source OAM order, while marker phase uses the application clock.

Inventory retains the gameplay HUD's displayed health, rupees, and dungeon
context through the existing status-bar owner. Its middle tilemap scrolls
between subscreens while the HUD and text bar stay fixed. The shared HUD layout
rules include two heart rows, the compressed layout above fourteen hearts, and
the two-button Biggoron sword display. Inventory cursor positions survive
closing; initialization resets only the source-defined essence/right-column
fields. Direction repeat advances on calls to the original direction handler,
and submenu initialization cannot consume the following input state early.

## Input contract

- The active modal exclusively consumes its controls; gameplay underneath does
  not see the same presses.
- Opening predicates include dialogue, transitions, story locks, room events,
  and other modal ownership.
- Menu input starts only after opening completes. A long host frame must not
  leak the opening press into the newly visible screen.
- Every controller in an original update reads the same immutable
  `ApplicationInputBuffer` snapshot. A just-pressed edge belongs to one update.
- Evaluate Start/Select chords before individual actions so both do not fire.
- Accepted and rejected navigation, selection, and opening actions request
  their original sounds at the traced update, not at an approximate visual
  moment.
- Map and inventory direction handlers share the retained autofire counter.
  Opening fades may transfer a held Start/Select chord to Save/Quit while keeping
  the same pause lease and fade progress. Closing map fades retain their last OAM.
- Presentation-only animation may use `AnimationPlayer`; original counters may
  not.

Dialogue that freezes gameplay participates in the same fixed-update ownership
rules even when it is not a full-screen modal. Preserve original object update
masks: some state-0 or explicitly enabled objects continue while ordinary
actors stop.

## Frontend ownership

The application owns one frontend controller from the clean-US Capcom screen
through the attract cinematic and title idle/replay states. It shares the same
`OracleRandom` instance later used by gameplay: ordered bird respawns consume
the first cinematic calls, and every title dispatch consumes one call before
its state handler. Starting gameplay must not reseed that owner.

Start remains gated until the Capcom stage finishes. During the cinematic it
enters title initialization in the same original update; at the title it
requests `SND_SELECTITEM $56` followed by `SNDCTRL_FAST_FADEOUT $fa`, completes
the source fade, and only then transfers ownership to file select.

## Adding or changing a menu

1. Trace the original screen state, input order, counters, fades, OAM/tilemap
   data, palettes, sounds, and state writes.
2. Import presentation facts rather than copying coordinates or graphics into
   controller code.
3. Give one controller the screen state and use the existing modal/pause owner
   where its lifecycle matches.
4. Keep item, ring, map, or save mutations in their authoritative state owner.
5. Validate opening and closing boundaries, direct screen switches, ownership
   failure, input-edge consumption, sounds, cancellation, and restoration when
   gameplay was already disabled.

File-select and forced save/game-over screens have specialized shell ownership
but follow the same evidence rules. Their disk writes must use the explicit
save operations described in [Saves and state](saves-and-state.md).
