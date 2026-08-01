# Development

## Requirements

- Godot 4.7.1 with .NET support
- .NET 8 SDK and PowerShell
- A clean US Oracle of Ages ROM with MD5
  `C4639CC61C049E5A085526BB6CAC03BB`
- A local `oracles-disasm` checkout

The current environment uses:

```text
Repository:     E:\Stuff\Github\ooa-godot
Disassembly:    C:\msys64\home\timst\oracles-disasm
Godot console:  E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe
```

Pass `-Rom` or `-Disassembly` to the importer for other locations. Never commit
the ROM.

## Common commands

Import or refresh generated assets:

```powershell
& .\tools\import_oracles.ps1
```

For importer infrastructure, parser, schema, or deterministic-output changes:

```powershell
& .\tools\verify_oracle_import.ps1
```

Build both production and validation assemblies:

```powershell
dotnet build
```

Run the normal game flow:

```powershell
& 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64.exe' --path .
```

Start directly in a hexadecimal room for development:

```powershell
& 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64.exe' --path . -- --group=4 --room=04
```

Project arguments must follow `--`. Direct room starts bypass retail file and
checkpoint progression. For a side-scrolling dungeon room, name its source
group (`4` or `5`); the development loader performs the retail active-group
switch to `6` or `7`.

Run all headless validations or one exact registered method:

```powershell
$godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate
& $godot --headless --path . --quit-after 10 -- --validate --validate-only=ValidateMethodName
```

See [Validation](validation.md) for scenario isolation and handoff checks.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | Arrow keys or WASD | D-pad/stick |
| A / sword | Z or K | A |
| B / equipped item | X or J | B |
| Start / inventory | I or Enter | Start |
| Select / map | M or Tab | Back |
| Save & Quit | Start + Select | Start + Back |

Development-only controls:

| Key | Action |
| --- | --- |
| F | Map/room fast travel; cycle group pages while open |
| F1 | Edit live flags, linked state, items, and appraised rings |
| F2 | Toggle Link collision |
| F3 | Arrange a normal Maple encounter |
| V | Warp to the configured debug room (default `4:11`) |
| Shift + 0-9 | Save a debug savestate |
| 0-9 | Load a debug savestate |

Override the V target with `--debug-warp-group=` and
`--debug-warp-room=`. Debug tools mutate live state and do not bypass the
project's explicit-save rules. Debug savestates are separate from the three
retail-compatible file slots.

## Change cycle

1. Inspect `git status --short` and preserve unrelated changes.
2. Trace the relevant ROM/disassembly behavior, including callers and tables.
3. Identify the authoritative importer and runtime owner.
4. Extend the importer before runtime code when generated data is incomplete.
5. Implement the behavior and focused regression together.
6. Regenerate affected assets and review their diff.
7. Run the appropriate import checks, `dotnet build`, the full headless suite,
   `git diff --check`, and `git status --short`.
8. Update a guide only if a durable rule changed; update
   [implementation status](implementation-status.md) only for a broad coverage
   change.

Use `rg` for repository and disassembly searches. Inspect pixel-sensitive work
at an integer scale; the internal viewport is 160 by 144.

## Continuous validation

[The validation workflow](../.github/workflows/validation.yml) rebuilds the
supported ROM from pinned public sources, verifies its MD5, runs importer
ownership and determinism checks, builds with warnings as errors, downloads the
pinned Godot .NET version, runs the complete headless suite, rejects Godot
warnings/errors, and runs `git diff --check`.

Version and checksum pins for Godot, WLA-DX, and the disassembly must change
together and pass the complete workflow. The temporary ROM is never uploaded as
an artifact.
