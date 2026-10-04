using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuChordPromotionRom()
    {
        int hostCase1 = 0;
        foreach (int initialMenu in new[] { 1, 2 })
        foreach (int chordUpdate in Enumerable.Range(1, 12))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(0, 0x45);
            _player.WarpTo(new Vector2(80, 88));
            Vector2 pausedPosition = _player.Position;
            var rom = new MenuRom(_saveData, _currentRoom);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            bool initialized = false;
            string context = $"Opening chord menu=${initialMenu:x2} update={chordUpdate} batch={batched}";
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    if (initialized) rom.AdvancePalette();
                    rom.Update(edge, held, ++update); initialized = true; edge = 0;
                    Phase expected = rom[0xcbcb] == 0 ? Phase.Closed : rom[0xcbcc] switch
                    {
                        0 => Phase.OpeningFadeOut,
                        1 => rom[0xc4ab] != 0 ? Phase.OpeningFadeIn : Phase.Open,
                        2 => Phase.ClosingFadeOut,
                        3 => Phase.ClosingFadeIn,
                        _ => throw new InvalidOperationException(context + ": unexpected native phase.")
                    };
                    FailIf(_menuLifecycle.CurrentPhase != expected ||
                        _gameplayPause.IsLeased != (rom[0xcbcb] != 0) || _player.Position != pausedPosition,
                        context + $" actual update={update}: ownership/phase/movement differs.");
                    if (expected is Phase.OpeningFadeIn or Phase.Open)
                        FailIf(_inventoryScreen.Visible != (rom[0xcbcb] == 1) ||
                            _mapScreen.Visible != (rom[0xcbcb] == 2) ||
                            _saveQuitScreen.Visible != (rom[0xcbcb] == 3),
                            context + $" actual update={update}: selected screen differs from native menu ${rom[0xcbcb]:x2}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $" actual update={update}: transfer changed cue order.");
                });
            }
            int open = initialMenu == 1 ? 8 : 4;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, open, open);
                if (chordUpdate > 1) Step(chordUpdate - 1, held: open);
                // menuStateFadeIntoMenu samples held keys. No fresh edge is
                // required, including its final dispatch at full white.
                Step(1, held: 12);
                Step(23 - chordUpdate);
                int expectedMenu = chordUpdate <= 11 ? 3 : initialMenu;
                FailIf(rom[0xcbcb] != expectedMenu || _menuLifecycle.CurrentPhase != Phase.Open,
                    context + ": promotion boundary did not establish the traced menu.");
                int close = expectedMenu == 1 ? 8 : 2;
                Step(1, close, close); Step(22);
                FailIf(_gameplayPause.IsLeased, context + ": repeated close retained the transferred lease.");
            }
        }
        GD.Print("Validated 25 clean-US opening chord promotion fixtures: held-only Start+Select on all eleven fade-out dispatches and the following fade-in update, Inventory/Map to Save/Quit ownership, screen swap, cue order, cancellation/repeat and representative split/batched gameplay.");
    }
}
