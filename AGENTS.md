# Oracle of Ages Godot Port — Agent Guide

## Project scope

This repository reconstructs *The Legend of Zelda: Oracle of Ages* in Godot 4.6/.NET using the original game data and engine behavior as reference.

- Project root: `E:\Stuff\Github\ooa-godot`
- Oracle disassembly: `C:\msys64\home\timst\oracles-disasm`
- Supported ROM: `Legend of Zelda, The - Oracle of Ages (U) [C][!].gbc`
- Godot executable: `E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe`

Treat the disassembly as the source of truth for timing, coordinates, collision radii, animation records, transitions, and game-specific behavior. Avoid approximating behavior when the relevant engine code or data can be traced.

## Repository layout

- `src/`: Godot C# runtime code.
- `tools/import_oracles.ps1`: imports data from the ROM and disassembly.
- `assets/oracle/`: generated runtime assets. Do not hand-edit generated files.
- `scenes/`: Godot scenes.
- `README.md`: current feature summary, controls, and development slices.

## Working rules

1. Preserve unrelated user changes. The worktree is commonly dirty between feature slices.
2. Use `apply_patch` for source and documentation edits.
3. Modify `tools/import_oracles.ps1` when imported data is incomplete or incorrect, then regenerate the assets.
4. Do not encode one-room exceptions when the original tables describe a general rule.
5. Keep gameplay coordinates in original room space. Screen-space UI belongs outside room-camera transforms.
6. Match original frame counts at 60 updates per second when the disassembly specifies counters.
7. Add or extend a headless validation for each regression or new gameplay system.
8. Update `README.md` when a player-visible feature or development control changes.

## Importing assets

Run the importer after changing its parsing or generated formats:

```powershell
& .\tools\import_oracles.ps1
```

The importer verifies the clean US ROM MD5 before generating assets. Its expected hash is:

```text
C4639CC61C049E5A085526BB6CAC03BB
```

If a generated binary format changes, update its runtime reader in the same change and verify the expected byte count.

## Building

```powershell
dotnet build
```

The build should complete with zero warnings and zero errors.

## Running

Launch the game from the repository root:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path .
```

Launch in a particular hexadecimal group and room by placing project arguments after `--`:

```powershell
& 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe' --path . -- --group=4 --room=04
```

## Headless validation

Use the console executable and `--quit-after` so validation runs terminate:

```powershell
$godot = 'E:\Stuff\Gamedev\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64_console.exe'
& $godot --headless --path . --quit-after 10 -- --validate-house-warp
```

Available validation flags include:

- `--validate-world`
- `--validate-transition`
- `--validate-symmetry-transition` (launch with `--room=22`)
- `--validate-signs`
- `--validate-npcs`
- `--validate-animations`
- `--validate-sword-bush`
- `--validate-house-warp`
- `--validate-cave-warps`
- `--validate-terrain`
- `--validate-health`
- `--validate-chests`

Run the validation directly related to the change plus nearby regression tests. For room transitions, run at least the startup, house, and cave validations.

Before handing off, also run:

```powershell
git diff --check
git status --short
```

Do not discard or rewrite unrelated modifications shown by `git status`.

## Disassembly references

Common starting points:

- Warp sources: `data/ages/warpSources.s`
- Warp destinations: `data/ages/warpDestinations.s`
- Dungeon layouts: `data/ages/dungeonLayouts.s`
- Dungeon metadata: `data/ages/dungeonData.s`
- Transition constants: `constants/common/transitions.s`
- Link state and warp behavior: `object_code/common/specialObjects/link.s`
- Tilesets: `data/ages/tilesets.s`
- NPC/interactions: `data/ages/interactions.s` and `object_code/ages/interactions/`

Search the disassembly with `rg` before relying on remembered behavior. Preserve hexadecimal identifiers in comments and validation errors when they correspond to original rooms, tiles, objects, or transition values.

## Implementation notes

- Small room layouts are 10×8 metatiles (160×128 pixels).
- Large room layouts are 16×11 metatiles (256×176 pixels).
- The visible gameplay area is 160×128; the complete viewport is 160×144 including the HUD.
- Dungeon screen neighbors come from dungeon floor layouts, not room-ID arithmetic.
- The HUD, dialogue, fade overlays, and debug room label use screen coordinates.
- Room entities, Link, terrain effects, and room textures use world coordinates and follow the room camera.
- Runtime databases should consume generated data rather than reparsing disassembly source files during play.

When an original system is only partially supported, keep unsupported state-dependent behavior safe and deterministic, and document the limitation instead of inventing quest progression.
