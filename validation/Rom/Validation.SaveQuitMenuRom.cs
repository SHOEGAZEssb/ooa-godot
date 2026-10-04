using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSaveQuitMenuRom()
    {
        int hostCase1 = 0;
        foreach (bool gameOver in new[] { false, true })
        foreach (int option in new[] { -1, 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.SetDeathRespawnPoint(0, 0x45, 0, 2, 0x58, 0x50);
            int deaths = option switch { 0 => 99, 1 => 998, 2 => 999, _ => 0 };
            save.WriteWramByte(0xc61e, (byte)((deaths % 100 / 10) * 16 + deaths % 10));
            save.WriteWramByte(0xc61f, (byte)(deaths / 100));
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _player.WarpTo(new Vector2(80, 88));
            // Set after initialization: the native Game Over menu sees depleted
            // live health; restarting gameplay later restores it independently.
            if (gameOver) save.WriteWramByte(WramAddress.wLinkHealth, 0);
            save.WriteWramByte(0xc622, 0xfe);
            save.WriteWramByte(0xc623, 0xff);
            var rom = new MenuRom(save, _currentRoom);
            int slot = (option + 1) % 3;
            rom[0xff9a] = (byte)slot;
            if (gameOver) BeginGameOver();
            else _inventoryMenu.OpenSaveImmediatelyForValidation();
            rom.OpenImmediately(3, gameOver: gameOver);
            var menu = _inventoryMenu;
            var screen = _saveQuitScreen;
            var scene = _scene;
            var application = new ApplicationValidationFixture(this);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            byte[]? written = null;
            void Step(int count = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(held ?? pressed), MenuRomActions(pressed), batched, () =>
                {
                    // Declared main-thread boundary: it increments this raw
                    // little-endian counter before dispatch, except while stopped.
                    if (!gameOver)
                        for (int address = 0xc622; address < 0xc626; address++)
                            if (++rom[address] != 0) break;
                    rom.AdvancePalette();
                    rom.Update(edge, held ?? pressed, ++update);
                    edge = 0;
                    string context = $"Save/Quit gameOver={gameOver} option={option} update={update}";
                    FailIf(save.ReadWramByte(0xc61e) != rom[0xc61e] || save.ReadWramByte(0xc61f) != rom[0xc61f],
                        context + ": BCD death count differs from native cap/carry.");
                    if (rom.ResetHandoff || rom.RestartHandoff)
                    {
                        FailIf(menu.IsActive || !scene.IsQueuedForDeletion() ||
                            rom.ResetHandoff != (_frontendIntro?.Stage == FrontendIntroStage.Title),
                            context + ": application reset/restart handoff differs from ROM.");
                        return; // Native thread initialization is outside this call.
                    }
                    Phase expected = rom[0xcbcb] == 0 ? Phase.Closed : rom[0xcbcc] switch
                    {
                        0 => Phase.OpeningFadeOut,
                        1 => rom[0xc4ab] != 0 ? Phase.OpeningFadeIn : Phase.Open,
                        2 => Phase.ClosingFadeOut,
                        3 => Phase.ClosingFadeIn,
                        _ => throw new InvalidOperationException(context + ": unexpected menu load state.")
                    };
                    FailIf(_menuLifecycle.CurrentPhase != expected || _gameplayPause.IsLeased != (rom[0xcbcb] != 0),
                        context + $": phase/lease {_menuLifecycle.CurrentPhase} != ROM {expected}.");
                    if (screen.Visible)
                        FailIf(screen.Cursor != rom[0xcbb5] || screen.DelayCounter != rom[0xcbb6],
                            context + $": cursor/delay {screen.Cursor}/{screen.DelayCounter} != ROM {rom[0xcbb5]}/{rom[0xcbb6]}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $": sounds {string.Join(',', sounds.Requests)} != ROM {string.Join(',', rom.Sounds)}.");
                    for (int address = 0xc622; address < 0xc626; address++)
                        FailIf(save.ReadWramByte(address) != rom[address], context + $": playtime ${address:x4} differs.");
                    if (rom[0xcbb3] == 2 && rom[0xcbb6] == 30 && rom[0xcbb5] != 0)
                    {
                        written = save.Serialize();
                        for (int offset = 0; offset < OracleSaveData.FileSize; offset++)
                            foreach (bool backup in new[] { false, true })
                                FailIf(written[offset] != rom.SavedByte(slot, 0xc5b0 + offset, backup),
                                    context + $": {(backup ? "backup" : "primary")} payload ${0xc5b0 + offset:x4} differs.");
                    }
                });
            }
            if (gameOver) Step(11, 0x89); // Fade consumes direction/confirmation.
            Step(1, 0xcf); // Up before Down/B/A/Start, even at cursor zero.
            Step(1, 0x8b); // Down before B/A/Start.
            Step(64, held: 0x80); // Save screen has no direction autofire.
            Step(1, 0xcf); // Up wins the same chord from cursor one.
            if (gameOver)
            {
                Step(1, 2); // B ignored.
                Step(1, 3); // B consumes A without selecting.
                Step(1, 0x10); // Horizontal directions ignored.
            }
            if (option < 0)
            {
                if (gameOver) { Step(1, 0x80); Step(1, 0x80); Step(1, 0x8b); Step(1, 0x40); Step(1, 0x40); }
                else
                {
                    Step(1, 3); // Normal B cancels before A.
                    Step(22);
                    FailIf(menu.SaveRequests != 0 || _saveWriteRequests != 0 || _gameplayPause.IsLeased,
                        "Save/Quit cancellation wrote a file or retained pause.");
                    // Repeat through actual Start/Select opening, not a direct reset.
                    Step(1, 12);
                    Step(22);
                    Step(1, 2);
                    Step(22);
                    continue;
                }
            }
            for (int cursor = 0; cursor < Math.Max(0, option); cursor++) Step(1, 0x80);
            Step(1, 0x0d); // A/Start confirm; Select is ignored.
            FailIf(menu.SaveRequests != (option > 0 ? 1 : 0) || _saveWriteRequests != (option > 0 ? 1 : 0) ||
                screen.DelayCounter != 30 || (option > 0) != (written is not null),
                "Save/Quit did not write exactly once before the $1e-update delay.");
            Step(29, 0xff); // Selection delay suppresses every input.
            FailIf(screen.DelayCounter != 1 || scene.IsQueuedForDeletion(),
                "Save/Quit completed before the thirtieth delayed update.");
            Step();
            if (!gameOver && option != 2) Step(22);
            FailIf(menu.IsActive || _gameplayPause.IsLeased,
                "Save/Quit final handoff retained modal ownership.");
            // Snapshot stays at the selection update; gameplay resume must not
            // imply another save. File-system generations have separate coverage.
            FailIf(menu.SaveRequests != (option > 0 ? 1 : 0), "Save/Quit repeated its save during closing.");
        }
        GD.Print("Validated clean-US normal/Game Over save input priority, boundaries, no autofire, B gates, death BCD carry/cap, stopped playtime, both raw save copies, exact $1e delay, cancellation/repeat and application handoffs in split/batched updates.");
    }
}
