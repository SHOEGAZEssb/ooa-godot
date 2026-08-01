# Saves and state

## Authoritative state

`OracleSaveData` owns the original 0x550-byte image copied between WRAM
`$c5b0-$caff` and SRAM. Typed accessors retain original offsets, widths,
encodings, masks, and aliases. Gameplay systems read and mutate this live image
through its authoritative owner rather than keeping clone-specific mirrors.

State outside the original file image belongs to an explicit runtime owner such
as `OracleRuntimeState`. Do not invent a save offset or persist transient state
for convenience.

The three file slots are:

```text
user://oracle_of_ages.sav
user://oracle_of_ages_2.sav
user://oracle_of_ages_3.sav
```

Each may have a `.bak` from the previous validated generation. Saves use a
temporary file, validation/readback, flush, and replacement so a corrupt write
does not destroy a known-good primary or backup. I/O failure returns a result
the save UI can present and retry.

## Explicit persistence

Flags, inventory, health, rupees, story state, and room state mutate the live
WRAM-style image. Ordinary mutations and application exit do not write a file.
Only traced save flows commit: Continue leaves the live changes uncommitted;
Save and Continue or Save and Quit commit at their original boundary.

Restart and death behavior must distinguish:

- live state before a save decision;
- the maintained death/checkpoint destination;
- the disk generation that was last explicitly committed;
- initialization that restores transient/depleted fields after load.

Do not create an autosave, arbitrary-position checkpoint, or event-local
recovery copy.

Development savestates are separate versioned clone-side files. They capture
the required live/runtime/RNG/room context only at stable gameplay boundaries
and reconstruct transient actors from room data on load. They never change a
retail slot, backup, or explicit-save count.

## Flags, rooms, and inventory

Global and room flags retain their original bit tables and aliases. Access them
through `OracleSaveData`; do not copy a persistent completion flag into an
event-local boolean. Directional dungeon state must use the active dungeon and
neighbors resolved by `RoomSession`, including high dungeon indices.

Checkpoints change only where imported destinations or traced room behavior say
they do. Ordinary scrolling and time travel preserve them unless the original
updates them.

`InventoryState` is a typed view over imported treasure behavior and save
fields. Every imported treasure variable/mode must have a checked
implementation or fail at startup with the treasure ID and source data. Unknown
values never default to zero or silently discard a write.

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
