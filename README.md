# Oracles: Godot reconstruction

This project reconstructs *The Legend of Zelda: Oracle of Ages* and prepares
the shared engine for *Oracle of Seasons* in Godot 4.7.1/.NET. Ages is playable;
Seasons currently provides its game-selector preview only.

The project aims to reproduce the supported clean US game exactly. ROM behavior
and `oracles-disasm` are the authority for data, timing, coordinates, object
order, RNG, collision, animation, audio, transitions, and state.

## Requirements

- Godot 4.7.1 with .NET support, with its executable directory on `PATH`
- .NET 8 SDK and PowerShell (PowerShell 7 on Linux)
- The pinned `external/oracles-disasm` Git submodule
- ROM build tools: MSYS2 on Windows; Bash and WLA-DX 10.6 on Linux
  (see [setup](docs/development.md#requirements))

Add the Godot executable's directory to `PATH`. The examples use the Windows
.NET console executable's original name,
`Godot_v4.7.1-stable_mono_win64_console.exe`. On other platforms, substitute
your installed executable's filename.

Initialize the disassembly after cloning (or clone with `--recurse-submodules`):

```powershell
git submodule update --init --recursive
```

The importer uses `external/oracles-disasm` by default. On first import it
builds the clean US ROM from a separate pinned disassembly checkout, verifies
MD5 `C4639CC61C049E5A085526BB6CAC03BB`, and caches it under `.cache/oracle-rom/`.
Subsequent imports reuse the verified cache. You do not need to provide a ROM.
The cache and generated assets are ignored by Git.

## Build and run

Import the source data, then build:

```powershell
& .\tools\import_oracles.ps1
dotnet build -t:Rebuild -p:Optimize=true
```

Use path overrides when needed. An explicit `-Rom` bypasses ROM building:

```powershell
& .\tools\import_oracles.ps1 -Rom 'D:\roms\ages.gbc' -Disassembly 'D:\src\oracles-disasm'
```

On Linux, run `pwsh -NoProfile -File tools/import_oracles.ps1`. The same
`-Rom` and `-Disassembly` options accept native Linux paths.

Generated files are written to `assets/oracle/ages/` and
`assets/oracle/seasons/preview/`, with a table manifest for each game pack.
Change the importer and regenerate them; never edit generated files directly.

Launch the game selector, then choose Ages for its title and file-select flow:

```powershell
Godot_v4.7.1-stable_mono_win64_console.exe --path .
```

Launch directly into a hexadecimal group and room for development:

```powershell
Godot_v4.7.1-stable_mono_win64_console.exe --path . -- --group=4 --room=04
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
& .\tools\validate_parallel.ps1 -Godot (Get-Command Godot_v4.7.1-stable_mono_win64_console.exe).Source
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
