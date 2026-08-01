# Oracle of Ages: Godot reconstruction

This is a playable, in-progress reconstruction of *The Legend of Zelda: Oracle
of Ages* in Godot 4.7.1/.NET.

The project aims to reproduce the supported clean US game exactly. ROM behavior
and `oracles-disasm` are the authority for data, timing, coordinates, object
order, RNG, collision, animation, audio, transitions, and state. The game is not
complete; see [implementation status](docs/implementation-status.md) for the
current high-level boundary.

## Requirements

- Godot 4.7.1 with .NET support
- .NET 8 SDK and PowerShell
- A local `oracles-disasm` checkout
- The clean US ROM with MD5 `C4639CC61C049E5A085526BB6CAC03BB`

The default setup expects the disassembly at
`C:\msys64\home\timst\oracles-disasm` and the ROM in the repository root as:

```text
Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc
```

The ROM is required to generate personal research assets. Do not commit it.

## Build and run

Import the source data, then build:

```powershell
& .\tools\import_oracles.ps1
dotnet build
```

Use path overrides when needed:

```powershell
& .\tools\import_oracles.ps1 -Rom 'D:\roms\ages.gbc' -Disassembly 'D:\src\oracles-disasm'
```

Generated files are written to `assets/oracle/`. Change the importer and
regenerate them; never edit generated files directly.

Launch the normal title and file-select flow:

```powershell
& 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64.exe' --path .
```

Launch directly into a hexadecimal group and room for development:

```powershell
& 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64.exe' --path . -- --group=4 --room=04
```

Arguments after `--` belong to the project. Direct room launches bypass normal
file and checkpoint progression and are not evidence of retail behavior.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | Arrow keys or WASD | D-pad/stick |
| A / sword | Z or K | A |
| B / equipped item | X or J | B |
| Start / inventory | I or Enter | Start |
| Select / map | M or Tab | Back |
| Save & Quit shortcut | Start + Select | Start + Back |

Development controls and launch options are in the
[development guide](docs/development.md).

## Validate

```powershell
dotnet build
$godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate
```

Gameplay changes require a focused regression and a passing full suite. See
[Validation](docs/validation.md).

## Contributing and documentation

Start with [Project principles](docs/project-principles.md), then use the
[documentation index](docs/README.md) to choose the guide for the subsystem you
are changing. [AGENTS.md](AGENTS.md) contains the concise implementation rules
used by coding agents and is equally useful as a contributor checklist.

The game renders at the Game Boy Color's 160 by 144 resolution and is normally
integer-scaled. Original game data and generated assets are intended for
personal, non-commercial research use.
