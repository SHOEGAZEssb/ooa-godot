# Oracle of Ages — Godot reconstruction

This project is an early, playable reconstruction of *Oracle of Ages* in Godot 4.6/.NET. It streams the present overworld from `oracles-disasm`, applies the original GBC background palettes and flip attributes, uses the expanded per-tileset metatile mappings and quarter-tile collision masks, animates Link from the disassembled sprite/OAM records, and supports scrolling room transitions and a basic sword action. The data pipeline covers all 1,536 expanded Ages room layouts and all 103 concrete tilesets for subsequent gameplay slices.

## Import the research data

The ROM and disassembly are intentionally kept outside the normal Godot resource graph. Rebuild the small development asset set with:

```powershell
./tools/import_oracles.ps1
```

The importer validates the clean US ROM MD5 (`C4639CC61C049E5A085526BB6CAC03BB`, matching the disassembly's `ages.md5`), parses tileset and palette metadata, and copies the expanded, address-independent assets from `C:\msys64\home\timst\oracles-disasm`. Both paths can be overridden with `-Rom` and `-Disassembly`.

## Play

- Move: arrow keys or WASD
- Sword: Z or K (gamepad A)
- Reserved item button: X or J (gamepad B)
- Development sign warp: T
- Development animated-tile rooms: Y (toggles water/lava)
- Development sword/bush test: B
- Development house-warp test: H

The internal viewport is the Game Boy Color's 160×144 resolution and is integer-scaled to 640×576. The generated assets and original game data are for personal, non-commercial research use.

The default room now has a directly testable transition: hold **Up** from Link's starting position to walk through the green staircase into room `01`. Walk back down through the same opening to return to room `11`. The southern staircase in room `11` also connects to room `21`.

The gameplay HUD is reconstructed from the original HUD tilemap, flags, palette, item sprites, digit tiles, and full/partial/empty heart tiles. Health now uses the original quarter-heart units (`$0c` for three hearts), syncs into the HUD, and terrain respawn hazards remove a half-heart before returning Link to his last safe tile. Until inventory is connected, the HUD still displays the implemented level-1 sword in A, an empty B slot, and `000` rupees.

Signs now use the original `$f2` metatile interaction and the complete 42-entry `signText.s` lookup table. Their dialogue uses the original `gfx_font`, textbox dimensions, placement rules, palette, directional glyphs, and blinking continue marker. Press **A or B** (Z/K or X/J; either gamepad face button) to advance pages or close the final page. Press **T** at any time to warp directly below the Rolling Ridge/Lynna City test sign in present-overworld room `2a`.

Animated background tiles are driven by the original 22 tileset animation groups, 74 independent tracks, 112 graphics-transfer headers, frame durations, and all three Ages animation sheets. This covers overworld water/flowers, waterfalls, whirlpools, currents, pollution, seaweed, lava, and dungeon animation. Press **Y** to toggle between a water-heavy test room (`b8`) and the lava room (`03`).

The level-1 sword uses Link's original animation-mode `$22` poses, the original `spr_swords` weapon cells and OAM compositions, `swordArcData` positions/collision radii, and the frame-accurate bush-breaking event. Standard overworld bushes (`$c5`) are replaced by ground (`$3a`) and immediately stop colliding. Press **B** to warp below a bush in room `69`, then swing with the normal A-button control (Z/K or gamepad A).

Tile and screen-edge warps are resolved from all 529 original `warpSources.s` and `warpDestinations.s` records, including cross-group overworld/interior travel. Press **H** to stand below the left house door in present room `47`; walk up to enter room `2:ea`, then walk down through the interior's bottom opening to return to the same exterior door.

Terrain now uses the original Ages tile-type and hazard tables for Link-facing behavior. Grass, puddles, stairs, and vines adjust walking speed; currents and conveyors push Link; water and lava create a short splash effect, subtract a half-heart, and respawn him at the stored room respawn anchor. Holes use the original active-tile sample, pull Link toward the exact sampled hole center with the same initial partial-control window, then play Link's `LINK_ANIM_MODE_FALLINHOLE` frames, delay damage until the animation finishes, and respawn him with a short recovery freeze. Cliff tiles start a basic ledge hop in their allowed direction. This also opens collision `$10` tiles for terrain handling instead of treating all hazards as walls.

For room-rendering development, hexadecimal group and room values can be selected after Godot's `--` separator, for example `-- --group=2 --room=ea`.

## Next implementation slices

1. Port further room objects and interactions from `data/ages/*Room.s` and `objects/ages`.
2. Refine terrain with full swim/diving item checks, ledge screen transitions, terrain-specific Link animations, and the original low-health/death handling.
3. Add inventory/items, enemies, dialogue, save data, and the present/past world state.
4. Translate music and sound-effect sequencing into Godot audio streams.
