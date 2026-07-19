# Saves and runtime state

## Original save image

`OracleSaveData` owns the original 0x550-byte image copied between WRAM
`$c5b0-$caff` and SRAM. It retains the `Z21216-0` verification string and the
original 16-bit checksum. Typed properties read and write original offsets so
systems share one authoritative state instead of clone-specific mirrors.

The project has three slots:

```text
user://oracle_of_ages.sav
user://oracle_of_ages_2.sav
user://oracle_of_ages_3.sav
```

Each slot may have a `.bak` containing the previous validated generation.
Saving serializes to a temporary file, validates it by reading it back, flushes
it, then replaces the primary while preserving the old valid primary as backup.
A corrupt primary must never overwrite a known-good backup. I/O and permission
failures are surfaced as a `SaveResult` so gameplay can remain on the save screen
and offer a retry.

## Live state is not autosaved

Room flags, inventory, health, rupees, story flags, minimap position, and other
ordinary mutations update the live WRAM-style image only. The game does not
write a file on every mutation or automatically on application exit. Continue
leaves changes unsaved; Save and Continue and Save and Quit explicitly commit.

Transient state that was outside the file image, such as `wUpgradesObtained`,
belongs in `OracleRuntimeState` or another explicit runtime owner. Do not place
it at an invented save offset merely to make persistence convenient.

Gasha maturity is the original little-endian 16-bit field at `$c65f-$c660`.
`OracleSaveData.AddGashaMaturity` performs the original saturating update;
successful Shovel digs add one, while breakable-tile effect bit 7 may apply the
source table's additional maturity before that item-owned increment.

## Flags, rooms, and dungeons

Global flags comprise 128 imported bits. Room flags retain the four original
aliased tables for groups `0/2`, `1/3`, `4/6`, and `5/7`. Access them through
`OracleSaveData`; do not copy flags into event-local completion booleans when
re-entry behavior should observe saved state.

Dungeon collectibles use the current dungeon supplied by `RoomSession`.
Small-key bytes and map/compass/boss-key bitsets must address the full dungeon
index, including dungeons 8 through 15. Never use dungeon zero as a default for
a generic chest or treasure behavior.

Death respawn fields store the maintained checkpoint, not Link's arbitrary
position at save time. Imported warp destinations and room-specific checkpoint
code decide when the checkpoint changes. Ordinary room scrolling and time
portals retain the existing checkpoint unless the original says otherwise.

## Inventory and treasure transactions

`InventoryState` is a typed view over imported treasure behavior and the save
image. Treasure variables and behavior modes are explicit bindings. Every
imported combination must have a tested implementation or fail at startup with
the treasure ID, variable, and mode. Unknown strings must never read as zero or
silently discard a write.

One treasure grant is one transaction. Internal mutations such as adding rupees
or seeds do not emit nested change notifications; the completed transaction
notifies and serializes observers once. This keeps HUD refreshes and persistence
hooks from seeing partial or duplicate commits.

The F1 debug editor follows the same ownership boundaries: linked-game state is
written through the typed `$c612` save accessor, and item grants select an
imported treasure object and pass it through `InventoryState`. Debug changes do
not bypass the live image or trigger an automatic disk save.

When adding a state field:

1. Find its real WRAM address, width, encoding, masks, and initial value.
2. Determine whether the address is part of the 0x550-byte file image.
3. Add a typed accessor with bounds and encoding checks.
4. Route every consumer through the authoritative owner.
5. Validate new-file defaults, load, mutation, explicit save, backup recovery,
   and any aliases or high-index bitset entries.
