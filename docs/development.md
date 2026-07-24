# Development workflow

## Requirements

- Godot 4.6 with .NET support.
- The .NET 8 SDK and PowerShell.
- A clean US Oracle of Ages ROM with MD5
  `C4639CC61C049E5A085526BB6CAC03BB`.
- A local `oracles-disasm` checkout.

The paths used by the current development environment are:

```text
Repository:     E:\Stuff\Github\ooa-godot
Disassembly:    C:\msys64\home\timst\oracles-disasm
Godot console:  E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe
```

Pass `-Rom` or `-Disassembly` to the importer when using different source
locations. Do not commit the ROM.

## Build and run

Import or refresh generated data:

```powershell
& .\tools\import_oracles.ps1
```

Build the production and validation projects:

```powershell
dotnet build
```

Run the normal title and file-select flow:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path .
```

Start directly in a hexadecimal group and room for development:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path . -- --group=4 --room=04
```

Project arguments belong after `--`. Direct room arguments bypass the normal
menu/checkpoint start and must not be mistaken for retail progression behavior.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | Arrow keys or WASD | D-pad/stick |
| A / sword | Z or K | A |
| B / equipped item | X or J | B |
| Start / inventory | I or Enter | Start |
| Select / map | M or Tab | Back |
| Save & Quit shortcut | Start + Select | Start + Back |

Development controls are intentionally separate from game behavior:

| Key | Development action |
| --- | --- |
| F | Fully revealed map and room fast travel; F cycles present, past, and interior groups 2-5 while open |
| F1 | Live global/room flag, linked-game, and item grant editor |
| F2 | Toggle Link collision; `NOCLIP` appears beside the room ID while disabled |
| F3 | Start a Maple encounter; uses the current eligible room or moves to an imported location in the current era |
| V | Warp to the configurable debug room; defaults to the D1 Essence room `4:11` |

F3 reloads an eligible current room in place. From any other room, it moves
Link to the first imported past location when currently in group 1, or to the
first imported present location otherwise. It raises the live kill counter to
the equipped-ring threshold and lets the normal room parser spawn Maple and
reset the counter; the encounter's meeting count, rewards, and other resulting
state therefore follow the normal explicit-save rules. F3 is ignored while an
encounter, transition, dialogue, menu, or room event is already active.

The V target uses hexadecimal launch arguments and is independent of the
initial room override:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path . -- --debug-warp-group=4 --debug-warp-room=11
```

In the F1 editor, Tab cycles through global flags, room flags, linked/items,
and appraised rings.
Use Up/Down to select a row, Left/Right to jump through global flags or imported
treasure variants (and to change the selected room/table on the room page), and
A to toggle a flag or the linked-game bit. On an item row, A grants that exact
imported treasure variant and parameter through the live inventory transaction;
on the ring page, Left/Right selects one of the 64 imported names and A grants
it to the appraised list. These changes affect the live WRAM-style state and
follow the normal explicit save rules. Grant a Ring Box on the item page, grant
the desired rings on the ring page, then use Vasu's list menu to place them in
the box before equipping them from the normal Inventory screen.

The F fast-travel screen uses the overworld map for present and past, then a
16-by-16 hexadecimal room grid for each interior group. Use the movement keys
to select a room and A to travel after choosing the desired group page.

## Normal change cycle

1. Inspect `git status --short` and preserve unrelated work.
2. Trace the relevant disassembly code and data.
3. Change importer code before generated files when source data is missing.
4. Regenerate assets and review unexpected generated changes.
5. Implement the runtime behavior and its headless regression together.
6. Run the checks in [Validation](validation.md).
7. Update documentation for changed contracts or player-visible coverage.

Use `rg` to search the repository and disassembly. Do not hand-edit files under
`assets/oracle/`, discard unrelated dirty-worktree changes, or approximate a
behavior that can be traced. The project renders at the GBC's 160 by 144
resolution and should be tested at integer scale when inspecting pixels.

## Continuous validation

[The validation workflow](../.github/workflows/validation.yml) runs on every
push and can also be started manually from GitHub Actions. A clean runner
rebuilds the supported US ROM from the pinned public `oracles-disasm` `master`
revision, verifies its MD5, switches the disassembly checkout to the pinned
`hack-base` revision used by the importer, and regenerates the ignored runtime
assets. It then downloads the checksum-pinned Godot 4.6 .NET build, treats C#
warnings as errors, runs the complete headless validation suite, rejects Godot
engine warnings or errors, and runs `git diff --check`.

The temporary source ROM is neither committed nor uploaded as an artifact. When
the project deliberately adopts a newer Godot, WLA-DX, or disassembly revision,
update its version, commit, and archive checksum pins in the workflow together
and confirm the full workflow remains green.
