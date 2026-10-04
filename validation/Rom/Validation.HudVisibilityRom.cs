using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHudVisibilityRom()
    {
        int cases = 0;
        int hostCase1 = 0;
        foreach (int maximum in new[] { 12, 57, 64 })
        foreach (var visibility in new[] { (Flag: 0x77, Tail: 0x10), (Flag: 0, Tail: 0x10), (Flag: 0, Tail: 0x20) })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, (byte)maximum);
            save.WriteWramByte(0xc6ab, (byte)maximum);
            InitializeTransientSession(save);
            LoadValidationRoom(0, 0x45);
            _inventory.GiveTreasure(TreasureId.Sword, 1);
            _inventory.GiveTreasure(TreasureId.Shield, 1);
            _inventory.EquipB(TreasureId.Sword);
            _inventory.EquipA(TreasureId.Shield);
            _saveData.WriteWramByte(0xc622, 0xf8);
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.LoadHudGraphics();
            rom.SaveGraphicsBeforeMenu();
            _inventoryMenu.OpenImmediatelyForValidation();
            rom.OpenImmediately(1);
            var sounds = _sound.AttachPlayRequestAudit();
            string context = $"HUD visibility max=${maximum:x2} flag=${visibility.Flag:x2} tail=${visibility.Tail:x2} batch={batched}";
            int updates = 0;
            void Step(int count)
            {
                application.Step(count, Vector2.Right, batched: batched, afterUpdate: () =>
                {
                    int frame = _saveData.ReadWramByte(0xc622);
                    rom.Update(0, 0, frame);
                    updates++;
                    FailIf(_hud.StatusBarHidden != (rom[0xcbe7] != 0) ||
                        _statusBar.DisplayedHealth != rom[0xcbe4] ||
                        _statusBar.DisplayedRupees != (rom[0xcbe6] & 15) * 100 +
                            (rom[0xcbe5] >> 4) * 10 + (rom[0xcbe5] & 15),
                        context + $": update {updates}, visibility or frozen/recovering caches differ.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $": update {updates}, recovery cue order differs.");
                    CompareHudPixelsRom(_hud, rom, context + $" update={updates}", displayImage: true);
                });
            }
            Step(1);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                // Declared caller inputs: scripts/ages/scripts.s writes $77;
                // other hide callers use priority $80. The native tail check
                // clears equipped OAM only when exactly four entries exist.
                rom[0xcbe7] = (byte)visibility.Flag;
                rom[0xff9f] = (byte)visibility.Tail;
                byte[] retainedOam = Enumerable.Range(0, 16).Select(i => rom[0xcb00 + i]).ToArray();
                rom.HideHud();
                _hud.HideStatusBar();
                FailIf(rom[0xcbe7] != 0xff || rom[0xcbe9] != 0,
                    context + ": hide did not publish the native update/refresh masks.");
                for (int offset = 0; offset < 64; offset++)
                    FailIf(rom.Tile(offset) != 0 || rom.HudAttribute(offset) !=
                        (visibility.Flag == 0x77 ? 0 : 0x80),
                        context + $": native cleared tile/attribute ${offset:x2} differs.");
                for (int offset = 0; offset < 16; offset++)
                    FailIf(rom[0xcb00 + offset] != (visibility.Tail == 0x10 ? 0xe0 : retainedOam[offset]),
                        context + $": native conditional OAM clear differs at ${offset:x2}.");
                CompareHudPixelsRom(_hud, rom, context + $" hidden cycle={cycle}", displayImage: true);
                if (cycle == 0) { _inventory.ApplyDamage(4); _inventory.AddRupees(7); }
                else { _inventory.Heal(4); _inventory.AddRupees(-5); }
                rom.CopySave(_saveData);
                Step(7); // Cross playtime wrap while health/money remain pending.
                Step(3);
                rom.ShowHud();
                _hud.ShowStatusBar();
                FailIf(rom[0xcbe7] != 0 || rom[0xcbe9] != 0xff,
                    context + ": show did not publish the deferred complete refresh.");
                Step(19); // Actual menu/HUD dispatch reloads and resumes global phase.
                FailIf(_statusBar.DisplayedHealth != _inventory.HealthQuarters ||
                    _statusBar.DisplayedRupees != _inventory.Rupees,
                    context + ": resumed caches failed to converge to live inventory.");
            }
            _inventoryMenu.CloseImmediatelyForValidation();
            cases++;
        }
        FailIf(cases != 10, $"HUD visibility fixture count changed: {cases}.");
        GD.Print($"Validated {cases} clean-US hidden/shown HUD pixel and deferred-refresh fixtures, priority/conditional OAM clearing, pending health/money, global wrap, ordered recovery cues and repeated hide/show through representative split/batched gameplay. Palette fades and LCD/DMA remain outside this comparison.");
    }
}
