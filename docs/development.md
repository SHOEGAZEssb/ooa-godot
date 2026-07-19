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
| T | Sign test warp |
| Y | Animated water/lava test rooms |
| B | Sword and bush test warp |
| H | House warp test |
| C | Chest test |
| G | Power Bracelet chest test |

In the F1 editor, Tab cycles through global flags, room flags, and linked/items.
Use Up/Down to select a row, Left/Right to jump through global flags or imported
treasure variants (and to change the selected room/table on the room page), and
A to toggle a flag or the linked-game bit. On an item row, A grants that exact
imported treasure variant and parameter through the live inventory transaction.
These changes affect the live WRAM-style state and follow the normal explicit
save rules.

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
