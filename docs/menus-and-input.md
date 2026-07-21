# Menus and input

## One modal owner

`OracleMenuLifecycle` owns the common map/inventory menu lifecycle. Only one
client may own it at a time. A client requests opening, swaps its screen at full
white, handles menu-specific input only in the open phase, and requests closing.
It must not independently freeze Link or animate the shared fade overlay.

The lifecycle phases are:

```text
Closed -> OpeningFadeOut -> OpeningFadeIn -> Open
Open   -> ClosingFadeOut -> ClosingFadeIn -> Closed
```

The common fast fade lasts exactly 11 original updates in each direction. The
`MenuFade` covers the full 160 by 144 screen, including the HUD, and the screen
swap occurs at full white. Timing-critical fade state uses the fixed-update
controller, not a generic Tween.

The ordinary room-warp fade covers only the gameplay field at screen y=16-143;
the status bar remains above it. Ordinary room dialogue likewise converts its
field-relative textbox positions into that display region: the source's upper
y=8 and lower y=80 placements appear at screen y=24 and y=96. Full-screen
menus and pregame screens use their own unshifted screen-space text layouts.

A menu-to-menu switch such as Inventory to Save & Quit can reuse ownership and
begin the new fade-in while already white. It must not release gameplay between
the two screens.

Normal inventory and map opening request `SND_OPENMENU` (`$54`) at the
full-white screen swap, not on the initial Start/Select input. Inventory tab
switches request that same sound when their 13-update scroll begins. Accepted
overworld cursor moves and dungeon-floor changes request `SND_MENU_MOVE`
(`$84`); a blocked dungeon-floor direction remains silent.

Every accepted directional input on each of the three inventory subscreens
requests `SND_MENU_MOVE` (`$84`). An A/B storage-slot swap, including an
unequip into an empty slot, requests `SND_SELECTITEM` (`$56`); successfully
equipping or unequipping a ring requests the same sound, while pressing A on a
non-ring secondary item remains silent.

## Inventory text marquee

The inventory's bottom text bar uses the original `showItemText2` indices.
Ordinary items and all three subscreens resolve `TX_09XX`; ring slots combine
the `TX_3040+ring` name with the `TX_3080+ring` description as `TX_30c1` does.
Changing to a different text index centers its first line immediately, holds it
for 40 original updates, and then scrolls the remaining lines as one marquee at
one character per 8 updates. Re-selecting the same text index does not restart
the marquee. Starting a 13-update page scroll clears the text bar, and the new
page's selection appears on its first normal menu update after the scroll.

Treasure display mode `$00` uses the original inventory HUD tiles to draw
`L-` plus the live low-nibble level beneath stored items and beside equipped
A/B items. This applies to every imported level-mode record, including swords,
bracelets, switch hooks, boomerangs, and feathers.

Treasure display mode `$01` draws the live packed-BCD amount as two digit
tiles, including a leading zero. Satchel records first resolve their selected
seed treasure, so the same rule keeps the HUD, equipped A/B icons, and stored
inventory icon synchronized after every accepted seed use.

## Vasu ring menus

`RingMenuController` owns `MENU_RING_APPRAISAL` and `MENU_RING_LIST` through
the shared lifecycle; `VasuShopEvent` waits for its completion callback. The
appraisal view retains the status bar at screen y=0-15, and graphics-register
state `$0f` draws the 16-row appraisal map at y=16-143. Its
textbox position `$02` consequently begins at screen y=96, between the map's
y=88 and y=136 borders. The ring-list view hides the status bar, occupies the
full 160 by 144 screen, and uses the special top two tile rows for the Ring Box.

Both views render the imported unappraised/appraised maps, flags, ring graphics,
inventory graphics, and original palettes. Sixteen rings occupy each page.
The `$0f/$10` graphics-register states put the main `w4TileMap` 16 pixels below
the screen origin: the list's Ring Box comes from the off-screen `$0200`
segment at the top, while selection row 2 appears at screen y=32. The original
`mapMenu_tileSubstitutionTable` entries copy the alternate off-screen layout so
L-1, L-2, and L-3 Ring Boxes expose exactly one, three, and five slots.
The list's centered ring-name line uses the cleared bank-1 `$9200-$93ff`
`showItemText2` graphics buffer above the separate description box; it must not
resolve those tile numbers through a static inventory sheet. Ring-menu sprite
coordinates are converted from stored OAM coordinates by the hardware X/Y
biases, including the `$3e/$56` list-cursor rows.
Select starts the adjacent page, requests `SND_OPENMENU` on its initialization
update, then moves both pages eight pixels on each of 19 original updates. The
list cursor flickers while choosing a ring; the Ring Box cursor remains visible
during that choice and flickers only while choosing a box slot. Page arrows,
Ring Box membership markers, the equipped `E`, page counts, ring number, and
TX `$3040+id` name / `$3080+id` description all derive from live inventory
state.

Appraisal debits the source price before revealing a ring. A new ring is added
to the obtained bitset after its description; a duplicate is removed and its
refund is applied only after the result wait. Ring-list selection moves or
removes a ring in the selected box slot without permitting duplicate slots.
Closing the list deactivates an equipped ring that is no longer in the box.

## Gameplay pause lease

`GameplayPauseController` provides exclusive pause/input ownership. Its lease
captures Link's processing, physics-processing, and room-debug visibility state,
then restores those exact values when the owning modal closes. Never blindly
enable Link on close: another system may have disabled him before the menu
opened.

A lease is owner-checked and disposable. Failed acquisition means another modal
already owns gameplay; the caller leaves its own state unchanged. Closing or
switching without ownership is an error, not a silent no-op.

The debug flag editor currently uses the same pause owner directly because it
has a different presentation lifecycle. Gameplay-owned submenus such as kid
name entry remain under their interaction controller. If they are consolidated
later, preserve their original update masks and screen boundaries rather than
forcing them through the map/inventory sequence.

## Input rules

- The active modal exclusively consumes its controls; room gameplay beneath it
  does not also observe those presses.
- Opening predicates include story locks, dialogue, transitions, room events,
  and ownership by another menu.
- Input begins after the opening fade has completed. A long rendered frame that
  consumes several fixed updates must not also leak an action into the newly
  opened screen.
- Start/Select chords are evaluated deliberately so the individual button
  actions do not also fire.
- Before `GLOBALFLAG_INTRO_DONE` `$0a`, a newly pressed Start, Select, or
  Start+Select chord leaves the normal menus closed and requests `SND_ERROR`
  (`$5a`) exactly once, matching the common `b2_updateMenus` gate.
- Presentation animation may use `AnimationPlayer` when it is not authoritative
  gameplay timing. Original counters remain fixed-update state.

Validate opening, closing, cancellation, direct menu switches, ownership
failure, a long host frame, and restoration when Link was already disabled.

## File-select palettes

The normal file-select, copy, and message-speed screens use imported `PALH_05`
background colors. Entering either erase selection or erase confirmation swaps
background palettes 2-6 to `PALH_06` (`paletteData58a0`); this includes the
live heart and death-counter tiles. File-menu sprite palettes are unchanged.
Returning from erase mode restores `PALH_05`.

`FileMenuPresentation` assembles the shared top/middle/bottom file-menu
tilemaps and draws the common decorative OAM list used by file select and the
in-game save/quit screen. Page-specific maps, cursor coordinates, palette
selection, and live file data remain owned by their screens.
