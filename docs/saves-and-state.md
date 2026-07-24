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

On game over, the packed-BCD death count at `$c61e-$c61f` increments and
saturates at 999. The live save image still contains zero health when the
forced menu appears. Continue restarts from the maintained death checkpoint
without committing that image. Save and Continue commits the zero-health image
before restarting, and Save and Quit commits it before returning to file
select. In every restart/load case, `initializeGame` semantics restore depleted
health to the saved maximum in live state; no separate recovery checkpoint or
clone-only autosave is introduced.

Gasha state is retained in its original contiguous save fields. Harvest flags
are at `$c64c` (bit 0: first nut harvested; bit 1: Gasha Heart Piece already
awarded), the 16 planted bits are `$c64d-$c64e`, per-spot kill counters are
`$c64f-$c65e`, and little-endian maturity is `$c65f-$c660`. Planting consumes
one packed-BCD seed, sets the selected planted bit, and resets that spot's
counter; completing the shrink animation clears the planted bit but retains
the grass/dirt/sand ground replacement only for the current room instance.
Ordinary re-entry restores the source soft-soil tile, so the spot is reusable.

`OracleSaveData.AddGashaMaturity` performs the original saturating update.
Ordinary room entry adds 5 (destination preloading and cutscene-only room swaps
do not), enemy kills add 3, successful Shovel digs add 1, and breakable-tile
effect bit 7 may apply its source-table addition before the Shovel increment.
`giveTreasure` also adds the imported amounts: Essence 150, an Ages Heart Piece
36, a trade item 100, and the requested Heart-refill parameter. A non-first
Gasha harvest subtracts 200 with an underflow clamp before its treasure grant;
therefore a Fairy or five-Hearts result can immediately add 24 or 20 again.

## Flags, rooms, and dungeons

Global flags comprise 128 imported bits. Room flags retain the four original
aliased tables for groups `0/2`, `1/3`, `4/6`, and `5/7`. Access them through
`OracleSaveData`; do not copy flags into event-local completion booleans when
re-entry behavior should observe saved state.

Dungeon collectibles use the current dungeon supplied by `RoomSession`.
Small-key bytes and map/compass/boss-key bitsets must address the full dungeon
index, including dungeons 8 through 15. Never use dungeon zero as a default for
a generic chest or treasure behavior.

Using a small-key door decrements that current-dungeon byte and notifies HUD
observers immediately. The unlocked state is stored in directional room-flag
bits `$01/$02/$04/$08`; dungeon doors set both the active room's direction bit
and the opposite bit in the neighbor resolved from the imported dungeon floor
layout. Re-entry derives tile `$a0` from those flags rather than an event-local
"opened" boolean. Dungeon breakable-wall actions with bit 7 set use the same
paired directional flags and layout neighbor; this includes Spirit's Grave's
`$68/$69` Ember walls.

Death respawn fields store the maintained checkpoint, not Link's arbitrary
position at save time. Imported warp destinations and room-specific checkpoint
code decide when the checkpoint changes. Ordinary room scrolling and time
portals retain the existing checkpoint unless the original says otherwise.

Story-owned map advice remains in its original file-image bytes. The first
Maku Sprout rescue writes low text byte `$d6` to `wMakuMapTextPast` at `$c6e7`,
sets global advice flag `$3f` and saved flag `$12`, increments
`wMakuTreeState` at `$c6e8`, clears present room `0:38` layout bit `$01`, sets
past room `1:48` layout bit `$01`, and retains room `1:38` gate bit `$80`.
Keep these writes separate: they select different map, room, and dialogue
re-entry behavior even though the rescue performs them in one completion path.
In particular, the past `1:48` layout bit exposes the `$d7` spot used by
`INTERAC_TIMEPORTAL_SPAWNER $e1:$02`; before the Seed Satchel is obtained, that
subtype activates immediately on the bottom exit from the rescue room.

The following present-room conversation writes low text byte `$4f` to
`wMakuMapTextPresent` at `$c6e6` and global advice flag `$3e`. Dropping the
Seed Satchel sets room `0:38` bit `$80` and stores the Link-relative drop X in
`wMakuTreeSeedSatchelXPosition` at `$c6eb`. Until room item bit `$20` is set,
re-entry recreates `TREASURE_OBJECT_SEED_SATCHEL_03` at that X and Y `$58`.
These bytes are authoritative save-image state, not event-local recovery data.

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

The saved/live rupee wallet is authoritative and updates immediately. The HUD's
transient displayed rupees are not save data; the status-bar controller advances
them by one per original update until they match the wallet.

Live health is authoritative under the same split. The HUD's transient displayed
health catches up using the original damage and healing cadence and is not saved.

Ring state remains in the original save image: the 64 unappraised entries begin
at `$c5c0`, the eight-byte `wRingsObtained` bitset begins at `$c616`, the five
Ring Box slots are `$c6c6-$c6ca`, `wActiveRing` is `$c6cb`, the Ring Box level
is `$c6cc`, the packed-BCD unappraised count is `$c6cd`, and
`wNumRingsAppraised` is `$c6ce`. Appraisal, list transfer, and equip operations
must update these fields as one live transaction and notify UI observers only
after their state is internally consistent.

Ring award counters also retain their source representation. Enemy kills use
the binary word at `$c620-$c621`, collected rupees use the two-byte BCD value at
`$c627-$c628`, Maple's counter is `$c641`, and all 16 Gasha-spot counters occupy
`$c64f-$c65e`. Reaching 1,000 kills or carrying the 10,000th collected rupee
sets global flag `$00` or `$01`; the rupee counter wraps as the BCD addition
does. The Slayer counter stops after its flag is set, but Maple/Gasha counters
and the three-point enemy-kill Gasha-maturity increment continue. The Gasha
Ring increments every spot twice rather than once.

Maple's persistent encounter byte is `wMapleState` at `$c644`. Its low nibble
is the capped meeting count used for movement variation and vehicle selection;
bit 4 marks the active Touching Book exchange, bit 5 marks that exchange
complete, and bit 7 records Maple's one-time Heart Piece. Global flag `$44`
distinguishes the first past-world greeting. A qualifying room load resets only
the kill counter at `$c641`; the meeting count increments only when a collided
encounter reaches its normal departure, not when Maple finishes an unhit flight.

The F1 debug editor follows the same ownership boundaries: linked-game state is
written through the typed `$c612` save accessor, item grants select an imported
treasure object, and ring grants set the corresponding appraised-ring bit
through `InventoryState`. Debug changes do not bypass the live image or trigger
an automatic disk save.

When adding a state field:

1. Find its real WRAM address, width, encoding, masks, and initial value.
2. Determine whether the address is part of the 0x550-byte file image.
3. Add a typed accessor with bounds and encoding checks.
4. Route every consumer through the authoritative owner.
5. Validate new-file defaults, load, mutation, explicit save, backup recovery,
   and any aliases or high-index bitset entries.
