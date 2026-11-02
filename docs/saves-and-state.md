# Saves and state

The live byte owner and disk I/O remain shared mechanisms. Ages-specific address
and save-layout catalogs and runtime initialization live under `src/Games/Ages/`.
Keep existing Ages save filenames, verification bytes and the Godot user-data
identity when renaming application assemblies. Seasons uses its own new-file
initialization, global-flag offsets and verification signature. Deserialization
checks the expected game; a Seasons store rejects all three Ages slot filenames.
Its file menu commits names and message speed, copies and erases using
`oracle_of_seasons.sav`, `oracle_of_seasons_2.sav` and `oracle_of_seasons_3.sav`.
Standard Seasons overworld sessions use the shared save/game-over decisions and
checkpoint initialization. Loading a checkpoint outside imported room content
fails with its native group/room; linked-game content remains unsupported.

Shared inventory and runtime accessors bind common named fields through the
generated WRAM layout. Raw WRAM operations used by native scripts retain
physical game addresses. Do not apply the typed translation twice or use an
Ages address as a raw Seasons field.

## Authoritative state

`OracleSaveData` owns the original 0x550-byte image copied between WRAM
`$c5b0-$caff` and SRAM. Typed accessors retain original offsets, widths,
encodings, masks, and aliases. Gameplay systems read and mutate this live image
through its authoritative owner rather than keeping clone-specific mirrors.

State outside the original file image belongs to an explicit runtime owner such
as `OracleRuntimeState`. Do not invent a save offset or persist transient state
for convenience.
Pending respawn, item-get, scripted-control and scripted-movement requests share
the native force-state byte. Their parameters use the corresponding native
unions. Player and companion dispatch consume or clear the same request at their
source phases. Presentation phases must not recreate a request after another
native owner clears or replaces it. An active handler retains later requests
until its own source path checks them or returns to normal dispatch.
Item-get completion reads the shared text and object-mask signals. Content
producers publish their original mask writes; they do not supply a private
completion predicate. Keep the object mask, menu-disable byte and combined
Link-collision/menu lock distinct, including their cancellation lifetimes.
Request consumption selects the native state without choosing its parameterized
mode. The initializer reads the live parameter on its own update; parameter-only
writes after initialization do not restart the active behavior.
Selected respawn modes share their invisible wait, reveal and recovery owner.
Their substate and wait/recovery counter use the live native object bytes;
raw writes select the same handlers and retain byte arithmetic.
Recovery preserves later requests for the next eligible normal update; it does
not perform a room-warp reset. Explicit warps retain their cancellation boundary.
Debug capture rejects live exclamation helpers until their counters and
presentation can be reconstructed; rebuilding the room must not silently drop
an admitted transient object.
Live screen-distortion interactions likewise reject capture and restore until
their LCD presentation can be reconstructed.
Active reserved push-block and key-door phases also reject capture. Restoring
a raw reserved tile actor requires reconstruction support before admission;
reloading the room alone cannot restore its moving body and completion phase.
Dungeon-key sprites also reject capture and raw restore until their own phase
can be reconstructed, including the interval after the door has already opened.

The application advances the live four-byte playtime counter once per main-thread
game dispatch, including cutscenes and gameplay menus. Foreground graphics-load
continuations and host-only resource preparation do not start another dispatch.

The three file slots are:

```text
user://oracle_of_ages.sav
user://oracle_of_ages_2.sav
user://oracle_of_ages_3.sav
```

Each has a `.bak` containing the same explicit-save image, matching the
original's two SRAM copies. Loads prefer a valid primary, then a valid backup;
existing invalid copies never become a new game automatically. Saves use
temporary files, validation/readback, flush, and replacement. I/O failure
returns a result the save UI can present and retry.

## Explicit persistence

Flags, inventory, health, rupees, story state, and room state mutate the live
WRAM-style image. Ordinary mutations and application exit do not write a file.
Only traced save flows commit: Continue leaves the live changes uncommitted;
Save and Continue or Save and Quit commit at their original boundary.

The application routes file-menu operations and gameplay commits through one
overridable slot-store boundary. The ordinary host uses `OracleSaveStore`;
validation hosts can provide isolated committed images while preserving the
same save decisions and normal saved-spawn behavior.

The game selector transfers individual committed 0x550-byte port saves, rather
than emulator SRAM dumps or development savestates. Import validates the selected
game and checksum before confirming replacement, then commits through the same
slot store, including its backup. Export writes a validated single file and
cannot target the managed slot files or backups. Neither operation initializes
gameplay or commits a live session.

Restart and death behavior must distinguish:

- live state before a save decision;
- the maintained death/checkpoint destination;
- the disk generation that was last explicitly committed;
- initialization that restores transient/depleted fields after load.

Do not create an autosave, arbitrary-position checkpoint, or event-local
recovery copy.

Game initialization preserves positive saved health. Zero or negative health
returns at half the maximum, rounded down to whole hearts, with a three-heart
minimum. This repairs the live image after the save/continue decision; it does
not rewrite the depleted image that was explicitly committed.
Ordinary saved-game initialization also gives Link 120 non-flashing object
updates of protection. Continue consumes the initialization dispatch; the
restored room's first object update starts on the following dispatch.
Saved-spawn initialization uses the CGB gameplay profile and applies the native
Advance Shop checkpoint correction before restoring companions or loading the
room. Imported preset masks preserve fields the original does not write.
Presentation brightness does not select a different hardware gameplay profile.

Development savestates are separate versioned clone-side files. They capture
the required live/runtime/RNG/room context only at stable gameplay boundaries
and reconstruct transient actors from room data on load. They never change a
retail slot, backup, or explicit-save count.
Restoring presentation preserves captured native update-disable gates. Rebuilding
a visible HUD must not execute the native show-status-bar command, which also
releases its update gate.
An occupied native special-object slot also requires a bound actor that declares
its current phase reconstructible. Pending initialization and unsupported phases
reject capture through both application controls and the direct capture API.
Restore checks the destination factory's reconstruction support before replacing
the current session or writing live owners; a valid payload alone does not admit
an unsupported actor or phase.
Both games share companion reconstruction admission. Grounded waiting and riding
states restore their animation frame from the native cursor and graphics identity,
retaining the remaining counter and consumed parameter without advancing time or
RNG. Presenters bind again after final native-memory restoration, before the rider
is attached. Ambiguous partial-upload poses, offscreen coordinate projections and
rider poses that require unsaved graphics history remain explicitly unsupported;
restore must not guess their presentation from the first matching frame.
The shared format tags the native game and validates the embedded save against
that game. Version 5 includes identity, lifetime and animation cursors for
interactions and native minecart items bound to memory. Reconstruction binds those saved slots
before placed content and skips fresh remembered-companion and Maple admission. Earlier formats are
rejected before session replacement: their interaction data cannot distinguish
an absent native actor from an actor formerly held in private presentation state.
Each game has independent debug slot filenames; existing Ages names remain
unchanged. Restore
publishes native memory before Seasons room selection so captured seasons select
the correct geometry, then restores live memory, RNG and room-walk state after
actor reconstruction. Loading cancels the old session's modal leases without
executing their gameplay callbacks. In-flight opening events are not serialized;
Seasons captures are admitted after the opening, subject to the ordinary stable
gameplay and modal checks.

New files apply the imported common initialization writes followed by the
standard, linked or hero variant. Game-transfer secrets supply their original
fields before these writes; child initialization and vine defaults run before
the explicit file creation commit. Deserialization preserves the saved image, including zero vine
positions and invalid checkpoints. Loading applies only initialization traced
to the original game; port-specific save migrations and state repairs are not
part of this boundary.

## Flags, rooms, and inventory

Global and room flags retain their original bit tables and aliases. Access them
through `OracleSaveData`; do not copy a persistent completion flag into an
event-local boolean. Directional dungeon state must use the active dungeon and
neighbors resolved by `RoomSession`, including high dungeon indices.

Checkpoints change only where imported destinations or traced room behavior say
they do. Ordinary scrolling and time travel preserve them unless the original
updates them.
The shared checkpoint recorder copies the live room modifier, byte coordinates,
remembered-companion fields and active Link page. It preserves the unused buffer
byte. An occupied companion slot does not establish that Link is mounted.
Reserved-slot motion retains native direction and 8.8 coordinate words without
overwriting subid, script or state bytes. Room identity belongs to the room
session; scrolling retains the existing vehicle instead of reconstructing an
incoming copy from private room metadata.
Ages room initialization publishes its layout/underwater/companion modifier;
Seasons retains the season selected by its initialization or transition owner.

`InventoryState` is a typed view over imported treasure behavior and save
fields. Scalar properties and inventory arrays access that same live byte image;
inventory mutations do not copy a private image back over native writes. An
isolated inventory owns an ordinary in-memory save image and uses the same path.
Every imported treasure variable/mode must have a checked
implementation or fail at startup with the treasure ID and source data. Unknown
values never default to zero or silently discard a write.

The modified disassembly's cross-game item fields are not retail save fields.
Check those bindings against the clean US ROM: seed selection has its own
inventory bytes, and Ages Boomerang/Feather ownership does not use upgrade
levels. Inventory writes must never alias the present room-flag table.

Upgrade flags at `$cca8` belong to `OracleRuntimeState`. Inventory reads that
shared byte, so debug savestates retain it while retail saves do not. The
original treasure-loss routine clears the regular ownership bit without
clearing this separate upgrade byte. Additional treasure grants follow the
imported clean-US table once, in source order, within the same transaction.

The native frame counter is the single live `wFrameCounter` byte. The shared
main-thread clock copies the incremented playtime low byte before dispatch;
object updates do not advance it. Clock writes retain byte wrapping
and avoid invalidating unrelated NPC story-state bindings. Native object writes
to the counter use that same byte and remain visible to later phases.

One grant, loss, purchase, ring operation, or other item mutation is one
transaction. Internal byte changes complete before observers receive one
notification, so the HUD and menus never see a partial state. Saved values are
authoritative; animated displayed rupees or hearts are presentation state and
catch up at original update boundaries.

## Adding a state field or transaction

1. Find the actual WRAM address, width, encoding, mask, aliasing, initialization,
   and all readers/writers in the disassembly.
2. Determine whether it belongs to the 0x550-byte file image or transient WRAM.
3. Add a typed, bounds-checked accessor to the authoritative owner.
4. Route every consumer through that owner and remove parallel state.
5. Group multi-field mutations into one consistent transaction.
6. Validate new-file defaults, live mutation, observer timing, explicit save,
   Continue/Save and Continue/Save and Quit behavior, backup recovery, high
   indices, aliases, re-entry, and reload.

Keep source-specific address maps and compound writes beside their owning code
and validation. This guide records the persistence contract, not an exhaustive
WRAM catalog.
