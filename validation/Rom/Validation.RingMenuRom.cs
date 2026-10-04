using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRingListMenuRom()
    {
        int hostCase1 = 0;
        foreach (int level in new[] { 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wRingBoxLevel, (byte)level);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.GrantAppraisedRingForDebug(0);
            _inventory.GrantAppraisedRingForDebug(1);
            var rom = new MenuRom(_saveData, _currentRoom);
            _ringMenu.OpenImmediatelyForValidation(RingMenuMode.List, static () => { });
            rom.OpenImmediately(4, 1);
            var sounds = _sound.AttachPlayRequestAudit();
            Vector2 position = _player.Position;
            int update = 0;
            void Step(int updates = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                application.Step(updates, Vector2.Right, MenuRomActions(held ?? pressed),
                    MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, held ?? pressed, ++update);
                    edge = 0;
                    FailIf(_player.Position != position || !_gameplayPause.IsLeased,
                        "Ring list leaked gameplay input while owning the pause lease.");
                    FailIf(_ringMenuScreen.PageTransitionActive != (rom[0xcbcd] == 2),
                        $"Ring list L{level} update {update}: native page scroll state differs.");
                    if (!_ringMenuScreen.PageTransitionActive)
                        FailIf(_ringMenuScreen.Page != rom[0xcbb6] || _ringMenuScreen.ListCursor != rom[0xcbb4] ||
                            _ringMenuScreen.BoxCursor != rom[0xcbbd] || _ringMenuScreen.SelectingList != (rom[0xcbce] == 1),
                            $"Ring list L{level} update {update}: page/list/box/mode runtime={_ringMenuScreen.Page}/{_ringMenuScreen.ListCursor}/{_ringMenuScreen.BoxCursor}/{_ringMenuScreen.SelectingList}, ROM={rom[0xcbb6]}/{rom[0xcbb4]}/{rom[0xcbbd]}/{rom[0xcbce]}.");
                    for (int slot = 0; slot < 5; slot++)
                        FailIf(_inventory.RingAt(slot) != rom[0xc6c6 + slot],
                            $"Ring list update {update}: box slot ${slot:x2} differs from ROM.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Ring list update {update}: sound requests differ: {string.Join(',', sounds.Requests)} != ROM {string.Join(',', rom.Sounds)}.");
                });
            }
            Step(1, 0x20); // Left at slot 0 is rejected.
            Step(1, 0x10);
            Step(1, 1);
            Step(1, 1); // Insert ring 0.
            Step(1, 1);
            Step(1, 1); // Selecting the same ring removes it.
            Step(1, 1);
            Step(1, 0x10);
            Step(1, 3); // A wins A+B on the list.
            Step(1, 1);
            Step(1, 4);
            Step(20, 0x10); // One init plus nineteen scroll updates; input blocked.
            Step(1, 1); // Native consumes this edge restoring the list.
            Step(1, 1); // Unowned ring clears the destination box slot.
            Step(1, 1);
            Step(1, 0x20); // Wrap left across a page.
            Step(20);
            Step(1, 2); // Cancel is blocked by the post-scroll dispatch.
            Step(1, 0x80);
            Step(1, 0x40);
            Step(1, 2); // Cancel selection without changing box contents.
            Step(1, 0x30); // Right wins a left/right chord in the box.
            Step(44, held: 0x10);
            Step(1, 1);
            Step(1, 0x10);
            Step(39, held: 0x10);
            Step(1, held: 0x10);
            Step(8, held: 0x10);
            _ringMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated clean-US ring-list capacities, box/list priority, insertion/removal, unowned-ring clearing, selection cancellation and 19-update page scrolls through split/batched application updates.");
    }
}
