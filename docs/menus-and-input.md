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
