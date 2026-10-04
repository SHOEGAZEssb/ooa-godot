using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRingMenuLifecycleRom()
    {
        int hostCase1 = 0;
        foreach (RingMenuMode mode in new[] { RingMenuMode.Appraisal, RingMenuMode.List })
        foreach (int count in new[] { 0, 1, 17, 64 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetGlobalFlag(GlobalFlag.IntroDone); save.SetGlobalFlag(8);
            save.SetTextSpeed(4); save.WriteWramByte(WramAddress.wRingBoxLevel, 3);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45); _entities.Clear();
            for (int ring = 0; ring < count; ring++)
                if (mode == RingMenuMode.Appraisal) _inventory.GiveUnappraisedRing(ring);
                else _inventory.GrantAppraisedRingForDebug(ring);
            _player.WarpTo(new Vector2(80, 88));
            var application = new ApplicationValidationFixture(this);
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.EnableVramDmaTransfers(); rom.LoadHudGraphics(); rom.LoadRoomTileset(); rom.LoadRoomGraphics();
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, completions = 0, openings = 0;
            string context = $"Ring lifecycle mode={mode} count={count} batch={batched}";
            void Step(int updates = 1, int pressed = 0)
            {
                int edge = pressed;
                application.Step(updates, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette(); rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622));
                    rom.AdvanceText(); edge = 0; update++;
                    FailIf(_ringMenu.IsActive != (rom[0xcbcb] == 4) ||
                        _gameplayPause.IsLeased != (rom[0xcbcb] == 4) ||
                        completions != openings - (rom[0xcbcb] == 4 ? 1 : 0),
                        context + $" update={update}: menu lease/completion boundary differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $" update={update}: runtime=[{string.Join(',', sounds.Requests)}], native=[{string.Join(',', rom.Sounds)}].");
                    if (_ringMenuScreen.Visible && rom[0xcbcc] is 1 or 2 &&
                        (_menuLifecycle.CurrentPhase == Phase.Open || _menuLifecycle.FadeUpdate > 0))
                    {
                        // Cleared wRingMenu.numPages ($cbb5) stays zero for
                        // an empty appraisal; list initialization writes four.
                        int pages = mode == RingMenuMode.List ? 4 : (count + 15) / 16;
                        FailIf(_ringMenuScreen.PageCount != pages || rom[0xcbb5] != pages,
                            context + $" update={update}: page count runtime={_ringMenuScreen.PageCount}, native={rom[0xcbb5]}, source={pages}.");
                        CompareRingFramePixelsRom(rom, mode, context + $" update={update}",
                            _menuLifecycle.CurrentPhase == Phase.Open ? null : _scene.MenuFade.Color.A);
                    }
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // Declared entry at Vasu's asm-call handoff. Native scripts,
                // conversation reachability and post-menu wait are separate.
                FailIf(!_ringMenu.Open(mode, () => completions++), context + ": opening rejected.");
                rom.OpenRingFromVasu((int)mode); openings++;
                FailIf(rom[0xcbcb] != 4 || rom[0xcbcc] != 0 || rom[0xcc8a] != 1,
                    context + ": native Vasu helper did not publish its menu/freeze request.");
                Step(22); Step(10);
                Step(1, 2); Step(22); Step(3);
                FailIf(_ringMenu.IsActive || _gameplayPause.IsLeased || completions != repeat + 1,
                    context + ": cancellation did not complete exactly once.");
            }
        }
        GD.Print("Validated nine clean-US ring opening/closing/repeat fixtures from executed Vasu asm-call entry: List/Appraisal, empty/single/page-boundary/full contents, actual shared fades, full interior fade/settled frames, text/cue order, pause lease and exact callback boundary through representative split/batched gameplay. NPC/script reachability, terminal LCD-disabled publication and GPU timing are outside this comparison.");
    }
}
