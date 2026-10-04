using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuRom() => RunPauseMenuRom(false);
    private void ValidatePauseMenuFadeColorsRom() => RunPauseMenuRom(true);

    private void RunPauseMenuRom(bool compareFadeColors)
    {
        int hostCase1 = 0;
        foreach (int menu in new[] { 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(0, 0x45);
            _player.WarpTo(new Vector2(80, 88));
            var rom = new MenuRom(_saveData, _currentRoom);
            if (compareFadeColors)
            {
                rom.LoadHudGraphics();
                rom.SeedRetainedSpritePalette(6, new[] { 0x0000, 0x001f, 0x03e0, 0x7c00 });
                rom.SeedRetainedSpritePalette(7, new[] { 0x7fff, 0x1d72, 0x5294, 0x475f });
                FailIf(_scene.MenuFade.Material is not CanvasItemMaterial material ||
                    material.BlendMode != CanvasItemMaterial.BlendModeEnum.Add,
                    "Menu fade must use the scene's additive material.");
            }
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            bool initialized = false;
            Vector2 pausedPosition = _player.Position;
            void Step(int updates = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(updates, Vector2.Zero, MenuRomActions(held ?? pressed),
                    MenuRomActions(pressed), batched, () =>
                {
                    // Palette thread runs before the resumed menu dispatch;
                    // a newly requested fade starts on the following update.
                    if (initialized) rom.AdvancePalette();
                    rom.Update(edge, held ?? pressed, ++update);
                    initialized = true;
                    edge = 0;
                    Phase expected = rom[0xcbcb] == 0 ? Phase.Closed : rom[0xcbcc] switch
                    {
                        0 => Phase.OpeningFadeOut,
                        1 => rom[0xc4ab] != 0 ? Phase.OpeningFadeIn : Phase.Open,
                        2 => Phase.ClosingFadeOut,
                        3 => Phase.ClosingFadeIn,
                        _ => throw new InvalidOperationException($"Unexpected native menu load state ${rom[0xcbcc]:x2}.")
                    };
                    FailIf(_menuLifecycle.CurrentPhase != expected || _gameplayPause.IsLeased != (rom[0xcbcb] != 0),
                        $"Pause menu ${menu:x2} update {update}: {_menuLifecycle.CurrentPhase}/lease={_gameplayPause.IsLeased} != ROM {expected} (${rom[0xcbcb]:x2}/${rom[0xcbcc]:x2}, palette=${rom[0xc4ab]:x2}).");
                    FailIf(_player.Position != pausedPosition,
                        "Menu opening/closing input changed Link's world coordinates.");
                    if (compareFadeColors && _menuLifecycle.FadeUpdate > 0 &&
                        expected is Phase.OpeningFadeOut or Phase.OpeningFadeIn or Phase.ClosingFadeOut or Phase.ClosingFadeIn)
                    {
                        Color overlay = _scene.MenuFade.Color;
                        int offset = Mathf.RoundToInt(overlay.A * 31);
                        FailIf(offset != rom[0xc2ff] || overlay.R != 1 || overlay.G != 1 || overlay.B != 1,
                            $"Menu ${menu:x2} fade update={update}: scene offset={offset}, native=${rom[0xc2ff]:x2}.");
                        foreach (bool sprite in new[] { false, true })
                        for (int palette = 0; palette < 8; palette++)
                        for (int shade = 0; shade < 4; shade++)
                        {
                            int original = rom.HudColor(sprite, palette, shade);
                            int actual = 0;
                            for (int channel = 0; channel < 3; channel++)
                            {
                                float component = ((original >> (channel * 5)) & 31) / 31.0f;
                                actual |= Mathf.RoundToInt(Mathf.Min(1, component + overlay.A) * 31) << (channel * 5);
                            }
                            int native = rom.FadingColor(sprite, palette, shade);
                            FailIf(actual != native,
                                $"Menu ${menu:x2} fade update={update} {(sprite ? "OBJ" : "BG")}{palette} shade={shade}: scene additive result=${actual:x4}, native=${native:x4}.");
                        }
                    }
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Pause menu ${menu:x2} update {update}: sound requests differ: {string.Join(',', sounds.Requests)} != ROM {string.Join(',', rom.Sounds)}.");
                });
            }
            int open = menu == 1 ? 8 : menu == 2 ? 4 : 12;
            Step(1, open);
            Step(10, 1); // Opening press cannot also equip or select.
            Step(1);
            Step(10, 1);
            Step(1);
            Step(1, menu == 1 ? 8 : 2);
            Step(10);
            Step(1);
            Step(10);
            Step(1);
            FailIf(_gameplayPause.IsLeased || !_player.IsPhysicsProcessing(),
                "Menu did not restore gameplay at its native closing boundary.");
            Step(1, open); // Repeat using retained menu cursor/repeat state.
            Step(22);
            Step(1, menu == 1 ? 8 : 2);
            Step(22);
        }
        GD.Print("Validated clean-US Start/Select/chord dispatch, eleven-update opening/closing fades, pause release, blocked fade input, sounds and repeated opening through split/batched application updates.");
        if (compareFadeColors)
            GD.Print("Compared all 64 published BG/OBJ RGB555 fade colors with the scene's additive material and live overlay on every interior fade update. GPU viewport output and PPU timing remain outside this comparison.");
    }
}
