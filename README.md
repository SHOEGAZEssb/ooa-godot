# Oracle of Ages - Godot reconstruction

This repository is a playable, in-progress reconstruction of *The Legend of
Zelda: Oracle of Ages* in Godot 4.6/.NET.

The primary goal is a 1:1 clone. The supported US ROM and `oracles-disasm` are
the source of truth for data, timing, coordinates, object order, RNG use,
collision, animation, audio, transitions, and story state. Implementations are
expected to reproduce that behavior rather than approximate the visible result.
See [Project principles](docs/project-principles.md) before changing gameplay.

The game is not complete. Current coverage includes the full room/tileset data
set, world navigation and transitions, title/file flow, WRAM-compatible saves,
HUD/dialogue/map/inventory foundations, the imported audio sequencer, several
enemy families, and early-game Impa, Maku Tree, Ralph, and Nayru sequences. See
[Implementation status](docs/implementation-status.md) for the maintained scope
summary and [TODO.md](TODO.md) for planned engineering work.

## Quick start

Requirements:

- Godot 4.6 with .NET support
- .NET 8 SDK and PowerShell
- A local `oracles-disasm` checkout
- The clean US ROM with MD5 `C4639CC61C049E5A085526BB6CAC03BB`

The importer defaults to
`C:\msys64\home\timst\oracles-disasm` and expects the ROM at the repository
root as `Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc`. Override either
path when needed:

```powershell
& .\tools\import_oracles.ps1
& .\tools\import_oracles.ps1 -Rom 'D:\roms\ages.gbc' -Disassembly 'D:\src\oracles-disasm'
dotnet build
```

Generated files are written under `assets/oracle/`. Do not edit them by hand;
change the importer and regenerate them. The ROM and disassembly are not part of
the normal Godot resource graph and must not be committed.

Launch the title and file-select flow from the repository root:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path .
```

For development, start directly in a hexadecimal group and room:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path . -- --group=4 --room=04
```

Project arguments must follow `--`. Direct room launches bypass normal file and
checkpoint progression.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | Arrow keys or WASD | D-pad/stick |
| A / sword | Z or K | A |
| B / equipped item | X or J | B |
| Start / inventory | I or Enter | Start |
| Select / map | M or Tab | Back |
| Save & Quit shortcut | Start + Select | Start + Back |

Development fast travel, flag editing, and test-room controls are listed in
[Development workflow](docs/development.md).

## Validation

Build before running the separate headless validation scene:

```powershell
dotnet build
$godot = 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate
```

Every gameplay regression or newly supported system should add a headless check.
See [Validation](docs/validation.md) for test design and the complete handoff
checklist.

## Documentation

The [documentation index](docs/README.md) links all engineering guides. Start
with these when working in a subsystem:

- [Data import](docs/data-import.md)
- [Runtime architecture](docs/runtime-architecture.md)
- [Rooms and entities](docs/rooms-and-entities.md)
- [Menus and input](docs/menus-and-input.md)
- [Saves and runtime state](docs/saves-and-state.md)
- [Graphics and audio](docs/graphics-and-audio.md)
- [Cutscene command runner](docs/command-runner.md)

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/` | Godot C# production runtime |
| `scenes/` | Stable Godot scene trees |
| `tools/import_oracles.ps1` | Stable import entry point |
| `tools/import_oracles/` | Staged ROM/disassembly importers |
| `assets/oracle/` | Generated runtime assets |
| `validation/` | Separate headless validation project and scene |
| `docs/` | Durable architecture and workflow documentation |

The internal viewport is the Game Boy Color's 160 by 144 resolution and is
normally integer-scaled. Original game data and generated assets are intended
for personal, non-commercial research use.
