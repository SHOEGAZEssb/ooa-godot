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

After building, run all headless validations with the standard eight workers,
or one exact registered method:

```powershell
$godot = 'E:\Stuff\Gamedev\Godot\Godot_v4.7.1-stable_mono_win64_console.exe'
& .\tools\validate_parallel.ps1
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
| F4 | Spawn enemies and item drops at room coordinates |
| V | Warp to the configured debug room (default `4:11`) |
| Shift + 0-9 | Save a debug savestate |
| 0-9 | Load a debug savestate |

The F4 spawner pauses gameplay. Up/down selects a field; left/right changes
the category, object ID/sub-ID variant, or coordinate (in eight-pixel steps).
M/Tab advances ten objects for faster browsing.
The sprite preview uses imported OAM and palettes without creating an actor or
advancing gameplay. Entries without imported preview graphics show `NO IMAGE`.
Coordinates start at Link's room position, including in large rooms. A/Z creates
one object per press; B/X or F4 closes the menu. The enemy list uses imported
names and classifications; entries without a standalone combat handler report
that limitation. NPCs, bosses, and room-event controllers are outside this tool's
scope. Enemy creation uses the shared 16-slot allocator and normal AI/RNG, with
room-completion counting and placed-enemy defeat bits disabled. Drops use their
normal collection behavior. Spawned objects are transient and disappear on room
reload; their ordinary gameplay effects can still change live state.

Override the V target with `--debug-warp-group=` and
`--debug-warp-room=`. Debug tools mutate live state and do not bypass the
project's explicit-save rules. Debug savestates are separate from the three
retail-compatible file slots.

On F1's items page, selecting `FLUTE_00`, `FLUTE_01`, or `FLUTE_02` switches
the companion to Ricky, Dimitri, or Moosh and grants its callable flute. Equip
it on A or B and play it in a supported present overworld room to summon that
companion. Normal shop/minigame Strange Flutes still require the forest quest.
Closing F1 after a companion change
in Nuun Highlands reloads its terrain and entities and moves Link to safe
ground. Quest progress is retained.

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
