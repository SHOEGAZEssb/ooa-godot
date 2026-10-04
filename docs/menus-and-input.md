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
Menu fade-in starts its counter at $20; returning to the room starts at $1e.
Both retain the eleven-update lifecycle, with distinct intermediate colors.

`GameplayPauseController` provides an exclusive, owner-checked lease. It saves
the exact processing/input state it suspends and restores that state on
release. Never blindly enable Link when a menu closes: another owner may have
disabled gameplay first. Failed acquisition leaves the caller unchanged.

Gameplay-owned submenus may use their interaction controller when that matches
the original mechanism. Do not force every prompt through the map/inventory
lifecycle; preserve its update masks, fade, and screen boundary.
Normal map and inventory closing resume gameplay on the update that releases
menu ownership. The post-menu return value observes the cleared menu state,
so an eligible stair can activate on that same update.
Toggle-floor cutscenes bypass normal menu dispatch throughout their handler,
including the update that releases the object freeze. Fresh menu input becomes
eligible on the following normal gameplay update.

## Screen-space boundaries

The Gale Seed menu shares the map presentation and menu lifecycle. It selects
only visited destinations from the imported ordered tree table, including the
present scent tree's planting flag. Confirming a destination transfers at white
to the room transition owner; canceling restores Link's falling state in the
existing room. Neither path uses development fast travel.
Accepted travel retains the map presentation during the ordinary warp fade.
Destination loading consumes the white update; the first following Link update
consumes its forced warping state and initializes the fall, before gravity starts.
That first object pass restores the ordinary menu caller. Falling and collapsed
Link can therefore pause during the remaining arrival fade. Opening a menu
replaces the arrival palette thread; closing completes the menu's room fade and
resumes the retained fall on that same update.

Full-screen menus and their fade use 160 by 144 screen space, including the
HUD. A room-warp fade covers only the gameplay field, normally at y=16-143. Ordinary room
dialogue starts with field-relative positions and adds the 16-pixel display
offset; pregame and full-screen presentations do not.

Dialogue placement derives from the source player/camera context and imported
position commands. Native events expose their screen context through
`IRoomEventDialogueContext`; cleanup restores normal selection. Do not move
the gameplay player merely to position a cutscene textbox.

Imported presentation records own source-ordered tilemaps, OAM, cursor
locations, palette selections, and layout data. Menu controllers own input,
cursor transitions, state changes, modal phases, and update timing. Apply Game
Boy OAM offsets, signed byte wrap, and hardware coordinate biases at the
rendering boundary instead of baking corrected coordinates into imported data.

The palette thread completes before ring-menu dispatch, so the update that
finishes opening also processes ring-menu input, text and sprites.
Ring menus capture sprite commands at their source dispatch boundaries. Box
cursors and C/E markers precede input, while list arrows and cursors follow
ordinary navigation. Confirmation, cancellation and page-selection dispatches
may omit the list sprites. Scroll updates freeze the list flicker counter;
closing fades retain the final captured OAM until the room is restored.
Ring number digits are retained background state. The source comparator
controls their refresh; changing cursor ownership or completing a page scroll
does not itself redraw them. Ring presentation composes the background, icons,
name and ordered OAM into the image used for drawing.
The same map upload can replace the fixed description background when it
targets $9800; uploading the alternating map retains those pixels. Replacing
the text thread does not itself erase the published panel. Closing retains
the final panel until the screen changes at white. Appraisal icon graphics
likewise refresh at the source redraw calls, preserving a removed ring's icon
through its result delay or closing fade.
Appraisal removes raw entries in place. Its delayed result redraw publishes the
new BCD count while retaining the original page count; reopening compacts the
entries and recalculates pages. Preserve these separate write boundaries rather
than deriving every displayed value from the current number of entries.
Page scrolling preserves the native window/BG split and window priority.
The window's hardware X bias places the incoming edge at 152 pixels. The
ring list's signed background below its text interrupt remains fixed while
the selection field moves; appraisal scrolls its page counter with the field.
Inventory composition also follows its IRQ's signed footer graphics and fixed
$9800 map while retaining SCX/WINX. Cursor submission follows the actual input
handler; page completion alone does not submit a cursor.

Map and inventory presentation read authoritative room, visit, inventory, and
HUD state. Keep cursor/repeat state and display animation with the menu owner;
initialization resets only source-defined fields.
Native map screens compose their background and ordered sprites into one
160-by-144 image for drawing, including the ten-sprite scanline limit. The
reusable background remains immutable between compositions; modal fade remains
owned by the shared lifecycle.
Inventory screens likewise compose their complete image before upload, keeping
the reusable backgrounds immutable and the HUD bound to its authoritative owner.
Inventory sprites preserve source submission order, background shade/priority
and the ten-sprite scanline limit. Submenu masks remain submitted on the
confirmation update, even after the panel closes. Empty storage cells still
redraw their source background tiles, whose shades control those masks.
Passive treasure rows all draw in table order; shared text slots do not merge
their graphics. Inventory's palette load retains OBJ6/7, so sprites using those
palettes read the live palette bytes from the authoritative runtime state.
Dialogue also composes the image used for drawing. Text scrolling preserves
the last published pixels while the source edits its map, clears the top tile
row on the next publication, then publishes the final whole-line position.
Completed non-exitable choices retain their published cursor until replacement.

Map marker and HUD recovery phases use the live playtime byte, including time
spent in menus that suspend their presentation updates. Inventory and ring
appraisal continue updating the status bar; maps, ring lists, and hidden status
bars suspend it without synchronizing away pending health or rupee animation.
The low-health warning uses live health and that same global phase. Eligible
normal-menu dispatch requests it before opening input; active menus, text,
instruments, death and the original disable masks suppress it. Its cadence
resumes on the retained global phase after closing.
Gameplay and inventory use the same composed HUD texture, including item OAM
and its background priority overlays. Inventory does not redraw equipped icons
through its storage-item renderer.

## Input contract

Item parents and physical children have independent lifetimes. Input priority,
slot allocation, initialization failure, and later parent completion are
separate phases. Concurrent items retain their own locks and counters; ending
one action must not release another's restriction. See
[Rooms and entities](rooms-and-entities.md) for native update ordering.

Allocate A before B, then update the resulting parents. A replacement must
take effect before the displaced item can spend ammunition or spawn a child.
Ordinary recoil still runs item input before knockback movement; it is not a
modal input lock.

- Project input actions bind keyboard, D-pad and left stick through the same
  application snapshot. Gamepad bindings accept any device index; the stick
  uses the movement actions' 0.25 deadzone independently for each direction.
  Movement is reconstructed from those digital pressed states, preserving
  intentional diagonals without letting below-threshold stick drift turn a
  cardinal press into a diagonal. Gameplay vectors and menu direction buttons
  therefore use the same deadzone.
- The active modal exclusively consumes its controls; gameplay underneath does
  not see the same presses.
- Opening predicates include dialogue, transitions, story locks, room events,
  and other modal ownership.
- Normal menus read the retained playing-instrument byte before the IntroDone
  cue gate. An empty Harp writes zero, allowing menu opening while its parent
  animation remains active; closing resumes that retained parent.
  Completed instrument bytes survive active dialogue until a subsequent normal
  Link update clears them, so closing text does not immediately permit menus.
- Normal gameplay advances electrical shock before menu dispatch. Its final
  counter update releases the shock restriction before that update's input;
  menu ownership does not suspend this cutscene phase.
- Menu input starts only after opening completes. A long host frame must not
  leak the opening press into the newly visible screen.
- Every controller in an original update reads the same immutable
  `ApplicationInputBuffer` snapshot. A just-pressed edge belongs to one update.
- Evaluate Start/Select chords before individual actions so both do not fire.
- Accepted and rejected navigation, selection, and opening actions request
  their original sounds at the traced update, not at an approximate visual
  moment.
- Map, inventory, and ring direction handlers share the retained autofire counter.
  Opening fades may transfer a held Start/Select chord to Save/Quit while keeping
  the same pause lease and fade progress. Closing map fades retain their last OAM.
- Presentation-only animation may use `AnimationPlayer`; original counters may
  not.

Dialogue that freezes gameplay participates in the same fixed-update ownership
rules even when it is not a full-screen modal. Preserve original object update
masks: some state-0 or explicitly enabled objects continue while ordinary
actors stop.

Closing ordinary text retains modal ownership for the source's separate closing
text update (`standardTextStatef` to `$10`). Object passes still observe active
text on that update; they resume on the next. Explicit cancellation through
`Close()` remains immediate.

Standard dialogue preserves preparation updates after opening, clearing at a
stop command, and scrolling. A stop continuation retains the old text on the
press update and clears it on the following preparation update. One A/B
continuation advances two new lines; the
second scroll is automatic. Revealing a line cannot also advance or close the
message with the same press. Any button can exit final standard text;
continuation text requires A/B and option prompts retain their own controls.

Option markers reserve printable space columns. After printing finishes, option
initialization and the text-speed-dependent cursor delay precede input. B selects
the last option; A confirms, with B taking priority in a chord. Horizontal moves
wrap in source option order; vertical moves select the nearest position on the
other row. Confirmation publishes the selection while text remains active through
the option exit and ordinary closing updates. Consumers take the result once
text releases ownership.

Non-exitable text retains its panel after publishing the source's `$80`
completed-printing signal. Menu-owned consumers may take a choice at that signal
and replace the panel before ordinary text closing. Appraisal waits begin only
after this signal; their decrement-to-zero update still returns before the
following update performs the result or exit action.

Inventory marquees retain their centered-name pause and character cadence
through description scrolling, blank spacing and name replay. A name that
fills all sixteen columns leaves its separator for the first scrolling update.
Presentation follows published glyphs; a staging-buffer shift without a
graphics upload must leave the displayed strip intact.
Item equip input bypasses selection-text dispatch for that update. Opening an
item submenu retains the existing request while its panel expands; the text
thread keeps advancing until ready-state input dispatches the option's text.

Ring-list name and description requests remain separate. Box text dispatches
before navigation, a changed name consumes its own dispatch and delay, and
unchanged descriptions continue printing without restarting. An owner's `$ff`
text cancellation restores the underlying menu while retaining the text thread
until its owner replaces or clears the request.

Text color commands select both glyph shades and palette attributes. Alternate
palette flags determine the initial attribute; an explicit color reset still
selects palette 0. NOCOLORS suppresses color commands. A/B line skipping bypasses
the skipped glyphs' individual sound effects and requests the ending character
cue subject to the shared text-sound cooldown.

Map palette headers write the authoritative background slots at the white
screen swap: present/past replace all eight, while dungeon maps replace only
slots 2-5. NoColors map text reads those live colors. Closing restores the
saved room palettes at white before gameplay resumes. Choice closing likewise
retains its published cursor while the text thread restores the WRAM map;
the following restore DMA removes the displayed panel and cursor together.

## Frontend ownership

File-menu mode changes defer screen initialization to the next original update.
Initialization replaces graphics and resets the cursor without accepting input.
Name confirmation similarly commits the file on its next dispatch, then reloads
file select on the following update. Each application update advances the sound
sequencer once, including initialization. File launch
waits until the update after the 32-step palette fade finishes and resets the
whole sound driver before starting the game's initial cue.

Before the original logos, a port-only loading screen presents
a black backdrop, Nayru's imported singing animation and alternating floating
music notes, with only a small progress bar. These presentation actors are
independent of room entities and never run the story event.
Its aspect-preserving 480 by 270 canvas
expands to the host display; exit and cancellation restore the original game
viewport before the logos. Its progress counts completed resource
preparation steps. Presentation runs on host time while the original frontend,
audio sequencer, input buffer, and shared RNG remain stopped. Once resources
are ready, it fades out and starts the original frontend at its first update;
loading time is never replayed as catch-up updates. Return-to-title bypasses
this startup screen. The new-game intro screen, dialogue resources and all its
sprite cells are also prepared and retained here; selecting a new file reveals
that screen and binds its name and text speed without rebuilding it. The original
360-update lead-in before the quest dialogue is unchanged. File-specific gameplay
preparation remains at file launch.
Pregame motion and flicker use the live playtime counter's byte phase after
the source initialization pass clears the frame counter. Sprite animation
retains its own counters; neither clock resets when the quest text opens.

Startup reads packaged data and tokenizes TSVs on a cancellable worker. It then
warms source graphics incrementally on the main thread; scene-tree changes and
GPU uploads never run on the data worker. Shared inputs survive file selection,
while mutable room layouts and file-dependent owners remain isolated. Menu
backgrounds use bulk pixel composition and HUD tiles share texture atlases to
avoid long per-pixel native-call and texture-upload batches during loading.

The application owns one frontend controller from the clean-US Capcom screen
through the attract cinematic and title idle/replay states. It shares the same
`OracleRandom` instance later used by gameplay: ordered bird respawns consume
the first cinematic calls, and every title dispatch consumes one call before
its state handler. Starting gameplay must not reseed that owner.

Frontend transitions retain original skip/input gates, fade endpoints, sound
order, and the handoff to file select. File-menu substates own their button
priorities and repeat timing; animated display copies never rewrite saved
health or other persistent fields.

The save screen's experimental Options entry is a port extension. Noclip uses
the F2 owner; room-overlay visibility edits the pause lease's restoration state.
These options do not write save bytes. Game over retains its original actions.
Room-overlay visibility and HUD placement persist separately in
`user://presentation.cfg` as soon as their options change. Missing preferences
default to a visible room overlay and a top HUD. Bottom placement
moves the gameplay field, its dialogue, reveal, fade and debug overlays up by
16 pixels without changing world coordinates or source camera state. Imported
full-screen menus retain their original layout. Temporarily hiding or fading
the status bar does not change its placement. Event-owned full-screen fades
continue to cover the entire viewport in either HUD layout.

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
