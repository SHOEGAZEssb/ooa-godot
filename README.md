# Oracle of Ages — Godot reconstruction

This project is an early, playable reconstruction of *Oracle of Ages* in Godot 4.6/.NET. It streams the present overworld from `oracles-disasm`, applies the original GBC background palettes and flip attributes, uses the expanded per-tileset metatile mappings and quarter-tile collision masks, animates Link from the disassembled sprite/OAM records, and supports scrolling room transitions, the map screen, and a basic sword action. The data pipeline covers all 1,536 expanded Ages room layouts and all 103 concrete tilesets for subsequent gameplay slices.

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
- Map / Select: M or Tab (gamepad Back); close with Select or B
- Development map fast travel: F; move, press A to travel, press F to switch eras, or B/Select to cancel
- Development sign warp: T
- Development animated-tile rooms: Y (toggles water/lava)
- Development sword/bush test: B
- Development house-warp test: H
- Development chest test: C

The internal viewport is the Game Boy Color's 160×144 resolution and is integer-scaled to 640×576. The generated assets and original game data are for personal, non-commercial research use.

An argument-free launch now starts in dungeon room `4:09`, a compact pushblock practice room with several original one-way blocks near Link. Tiles `$18`, `$19`, `$1a`, and `$1b` move only up, right, down, and left respectively; hold the matching cardinal direction against one for 20 updates to push it. Restart the game to restore the room after testing. Explicit `--group`/`--room` arguments and headless validations keep their previous defaults.

The gameplay HUD is reconstructed from the original HUD tilemap, flags, palette, item sprites, digit tiles, and full/partial/empty heart tiles. Health now uses the original quarter-heart units (`$0c` for three hearts), syncs into the HUD, and terrain respawn hazards remove a half-heart before returning Link to his last safe tile. Until inventory is connected, the HUD still displays the implemented level-1 sword in A and an empty B slot; its rupee counter now tracks collected chest rewards.

The Select map menu uses the original present, past, and dungeon 20x18 tilemaps, attributes, graphics pieces, palette headers, dungeon blurbs, 8x8 floor layouts, room-property connection tiles, 14x14 overworld cursor wrapping, 32-update location-marker blink, and 11-update fast white fades. Rooms become visible when entered during the current session; indoor maps retain the most recent exterior position, and dungeon floors can be selected with Up/Down after visiting them. Link and room animation freeze while the map is active. For development, **F** opens a fully revealed overworld map from any room, including dungeons: move to a destination, press A to load it at the white midpoint of the menu fade, or press F again to switch between the present and past maps. Map popup icons, A-button region text, inventory map/compass reveals, treasure/boss symbols, and persistent visit flags remain deferred until their quest, inventory, and save-state owners exist.

Signs now use the original `$f2` metatile interaction and the complete 42-entry `signText.s` lookup table. Their dialogue uses the original `gfx_font`, textbox dimensions, placement rules, palette, directional glyphs, and blinking continue marker. Press **A or B** (Z/K or X/J; either gamepad face button) to advance pages or close the final page. Press **T** at any time to warp directly below the Rolling Ridge/Lynna City test sign in present-overworld room `2a`.

The NPC importer now extracts all 377 positioned NPC/character interactions from the Ages room object table. It preserves `var03`, resolves the exact interaction/subid graphics record, follows the original animation and OAM pointer tables, exports complete per-facing idle sequences with their original frame durations and 8×16 tile compositions (including flips), applies the six standard sprite palettes, and copies every referenced sprite sheet. Link now also uses the original standard sprite palette 0 instead of an approximate recolor. Supported talkable NPCs that use `npcFaceLinkAndAnimate` now watch Link inside the original Manhattan-distance `$28` radius, wait 30 frames between turns, face down again when he leaves, and switch in front of Link at the original strict `npc.y > link.y + $0b` draw-priority boundary. Scrolling transitions retain the outgoing room's NPCs and load the destination NPCs before the camera starts moving, so both sets scroll with their respective rooms instead of vanishing or popping in. Dialogue is emitted only where a subid-specific script can be resolved statically; state-driven dialogue remains unassigned instead of borrowing an unrelated character's first text. Room `0:48` provides the primary animated/facing villager test and opens `TX_1420` with the existing textbox; room `0:66` covers the Link/NPC overlap case.

Animated background tiles are driven by the original 22 tileset animation groups, 74 independent tracks, 112 graphics-transfer headers, frame durations, and all three Ages animation sheets. Graphics-transfer writes now persist in their separate VRAM destination ranges, as required by the interleaved waterfall sequences, and all animation counters freeze during scrolling and room-warp transitions. This covers overworld water/flowers, waterfalls, whirlpools, currents, pollution, seaweed, lava, and dungeon animation. Press **Y** to toggle between a water-heavy test room (`b8`) and the lava room (`03`).

The level-1 sword uses Link's original animation-mode `$22` poses, the original `spr_swords` weapon cells and OAM compositions, `swordArcData` positions/collision radii, and the frame-accurate bush-breaking event. Standard overworld bushes (`$c5`) are replaced by ground (`$3a`) and immediately stop colliding. Press **B** to warp below a bush in room `69`, then swing with the normal A-button control (Z/K or gamepad A).

The importer preserves all 133 records from `chestData.s`. Closed chest metatiles (`$f1`) only open when approached from below; other directions use `TX_510d`. Supported rupee chests change to `$f0`, raise the original `spr_common_items` reward at `SPEED_40` for 32 frames, add the disassembled rupee amount to the HUD, display the matching `TX_00XX` pickup text, and remain open for the session. Chests containing inventory items remain closed with a development message until those item handlers exist. Press **C** to warp below the 30-rupee chest at room `0:49/$51`.

Ordinary pushable metatiles now use the original collision-mode records from `interactableTiles.s` and `pushableTiles.s`, including one-way and all-direction blocks, statues, and colored variants. Link must push cardinally for the original 20 updates; while pressing a block or an ordinary wall he uses the original `$64-$67` / `$90-$93` two-frame pushing animation, alternating every 6 updates. A clear destination then converts the background tile into a blocking object that moves 16 pixels at `SPEED_80` over 32 updates, restores the table-selected source floor, and places the table-selected destination tile. Water and lava consume the block with the matching splash, while holes consume it without replacing the hazard. Bracelet-gated tiles remain immovable until inventory is implemented; Somaria blocks, pushblock puzzle triggers, secret sounds, and the falling-down-hole object animation remain later interaction/audio slices.

Run the complete headless regression suite with `--validate`; the runner selects each scenario's required group and room automatically.

Tile and screen-edge warps are resolved from all 529 original `warpSources.s` and `warpDestinations.s` records, including cross-group overworld/interior travel. Room teleports reproduce the original transition modes: gradual or instant white-out, the 32-frame fade into the destination, Link's 28-frame scripted interior entry, and the 16-frame walk offscreen when leaving. Large `256x176` cave and dungeon rooms use a clamped playfield camera while the HUD and textbox remain fixed on screen. Their screen neighbors come from the original 8x8 dungeon floor layouts rather than overworld room-number arithmetic. Press **H** to stand below the left house door in present room `47`; walk up to enter room `2:ea`, then walk down through the interior's bottom opening to return to the same exterior door.

Terrain now uses the original Ages tile-type and hazard tables for Link-facing behavior. Grass, puddles, stairs, and vines adjust walking speed; currents and conveyors push Link. Water and lava create their original `INTERAC_SPLASH` or `INTERAC_LAVASPLASH` visual effect and play Link's 22-update `LINK_ANIM_MODE_DROWN` sequence, including its direction-specific entry pose and submerged frame; Link then disappears for two updates, loses a half-heart when he reappears at the stored room respawn anchor, and has a short recovery freeze. Holes use the original active-tile sample, pull Link toward the exact sampled hole center with the same initial partial-control window, then play Link's `LINK_ANIM_MODE_FALLINHOLE` frames, delay damage until the animation finishes, and respawn him with a short recovery freeze. Cliff tiles start a basic ledge hop in their allowed direction. Cardinal movement also uses the original eight-point adjacent-wall bitset and `tileEdgeAdjust` masks, causing Link to slide upward or downward along bridge ends and other partial tile edges instead of stopping dead. This also opens collision `$10` tiles for terrain handling instead of treating all hazards as walls.

For room-rendering development, hexadecimal group and room values can be selected after Godot's `--` separator, for example `-- --group=2 --room=ea`.

## Runtime architecture

`GameRoot` is limited to scene composition, lifecycle updates, HUD synchronization, and validation forwarding. Gameplay ownership is split by responsibility:

- `RoomSession` owns the active group/room and resolves overworld or dungeon neighbors.
- `MapMenuController` and `MapScreen` own Select-menu fades, input freezing, visit visibility, and original overworld/dungeon map rendering.
- `RoomTransitionController` owns warps, scrolling, fades, destination placement, and the room camera.
- `RoomEntityManager` and `InteractionController` own NPC lifetime, blocking, signs, and dialogue.
- `RoomCollision`, `TerrainController`, `PushBlockController`, and `CombatController` own collision, terrain behavior, moving blocks, and weapon effects.
- `PlayerWorld` implements the narrow `IPlayerWorld` API consumed by `Player`.
- Development launch options and test-room warps live under `src/debug`; headless regression scenarios live under `src/validation`.

## Next implementation slices

1. Port further room objects and interactions from `data/ages/*Room.s` and `objects/ages`.
2. Refine terrain with full swim/diving item checks, ledge screen transitions, terrain-specific Link animations, and the original low-health/death handling.
3. Expand the NPC/object system, then add inventory/items, enemies, save data, and the present/past world state.
4. Translate music and sound-effect sequencing into Godot audio streams.
