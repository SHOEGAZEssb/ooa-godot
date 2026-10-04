using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapFadeFramesRom() => RunMapPaletteLifetimeRom(true);

    private void RunMapPaletteLifetimeRom(bool compareFrames)
    {
        int hostCase1 = 0;
        foreach (int mode in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(mode == 2 ? 4 : mode, mode == 2 ? 0x46 : 0x33); _entities.Clear();
            _player.WarpTo(new Vector2(80, 64));
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.EnableVramDmaTransfers(); rom.LoadHudGraphics(); rom.LoadRoomTileset(); rom.LoadRoomGraphics();
            if (mode == 2) rom.LoadDungeon(2);
            // A previous textbox can replace only BG1. Distinct primary and
            // mixed vectors expose preserving that slot in the dungeon header
            // and restoring it after the full present/past map replacement.
            int[] retained = [0x001f, 0x03e0, 0x7c00, 0x294a];
            rom.SeedBackgroundPalette(1, retained);
            Color[,] live = _rooms.World.BackgroundPalettes.Capture();
            for (int shade = 0; shade < 4; shade++)
                live[1, shade] = new Color((retained[shade] & 31) / 31f,
                    ((retained[shade] >> 5) & 31) / 31f, ((retained[shade] >> 10) & 31) / 31f);
            _rooms.World.BackgroundPalettes.Restore(live);
            int update = 0;
            void Compare()
            {
                for (int palette = 0; palette < 8; palette++)
                for (int shade = 0; shade < 4; shade++)
                {
                    Color color = _rooms.World.BackgroundPalettes.Resolve(palette, shade);
                    int runtime = Mathf.RoundToInt(color.R * 31) | Mathf.RoundToInt(color.G * 31) << 5 |
                        Mathf.RoundToInt(color.B * 31) << 10;
                    FailIf(runtime != rom.HudColor(false, palette, shade),
                        $"Map palette mode={mode} batch={batched} update={update} BG{palette}/{shade}: runtime=${runtime:x4}, native=${rom.HudColor(false, palette, shade):x4}, menu=${rom[0xcbcb]:x2}/${rom[0xcbcc]:x2}.");
                }
            }
            void Step(int count = 1, int press = 0)
            {
                int edge = press;
                application.Step(count, Vector2.Zero, MenuRomActions(press), MenuRomActions(press), batched, () =>
                {
                    rom.AdvancePalette(); rom.Update(edge, press, _saveData.ReadWramByte(0xc622)); edge = 0; update++;
                    Compare();
                    FailIf(_mapMenu.IsActive != (rom[0xcbcb] != 0), "Map palette lifecycle changed modal ownership.");
                    if (compareFrames && _mapScreen.Visible && rom[0xcbcc] is 1 or 2 &&
                        (_menuLifecycle.CurrentPhase == Phase.Open || _menuLifecycle.FadeUpdate > 0))
                    {
                        FailIf(_scene.MenuFade.Material is not CanvasItemMaterial material ||
                            material.BlendMode != CanvasItemMaterial.BlendModeEnum.Add,
                            "Map fade lost its additive scene material.");
                        using Image frame = _mapScreen.ComposeImage();
                        CompareMenuFramePixelsRom(rom, frame,
                            $"Map faded frame mode={mode} batch={batched} update={update} fade={_scene.MenuFade.Color.A}",
                            additiveFade: _menuLifecycle.CurrentPhase == Phase.Open ? null : _scene.MenuFade.Color.A);
                    }
                });
            }
            Compare();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, 4); Step(22); Step(5);
                Step(1, 2); Step(22); Step(4);
                FailIf(_mapMenu.IsActive || _gameplayPause.IsLeased, "Map palette restoration retained its menu lease.");
            }
        }
        GD.Print("Validated four clean-US live map-palette lifecycles: PALH_07/$08 full replacement, PALH_09 partial BG2-5 load with retained textbox BG1, native room palette load/restoration at white, all 32 RGB555 colors, complete opening/closing/repeat and representative split/batched gameplay.");
        if (compareFrames)
            GD.Print("Compared complete composed map frames plus the configured additive scene fade with native fading BG/OBJ colors on every interior visible fade update, settled/completed map and repeat. The full-white loader disables LCD and bounded LCD-off calls omit its cooperative yields; terminal swap publication, GPU output and hardware timing remain outside this comparison.");
    }
}
