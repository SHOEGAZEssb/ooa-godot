using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHudRom()
    {
        // Probe the actual composed tilemap, before rasterization. This private
        // method has no production operation equivalent to reading WRAM tiles.
        var buildMap = typeof(Hud).GetMethod("BuildStatusMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (int maximum in new[] { 12, 28, 32, 56, 60, 64 })
        foreach (int health in Enumerable.Range(0, maximum + 1))
        foreach (int flags in new[] { 0, 8, 0x18 })
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(WramAddress.wLinkMaxHealth, (byte)maximum);
            save.WriteWramByte(WramAddress.wLinkHealth, (byte)health);
            save.WriteWramByte(WramAddress.wNumRupees, 0x98);
            save.WriteWramByte(WramAddress.wNumRupees + 1, 9);
            save.WriteWramByte(WramAddress.wDungeonSmallKeys + 1, 7);
            var inventory = new InventoryState(_treasures, save);
            var rom = new MenuRom(save, _currentRoom);
            rom[0xcc34] = (byte)flags;
            rom[0xcc39] = 1;
            var hud = new Hud();
            AddChild(hud);
            try
            {
                hud.Initialize(_treasures, inventory);
                hud.MaxHealthQuarters = maximum;
                hud.HealthQuarters = health;
                hud.Rupees = 998;
                hud.TilesetFlags = (byte)flags;
                hud.DungeonIndex = 1;
                rom.UpdateHud(0);
                var map = (byte[])buildMap.Invoke(hud, null)!;
                for (int row = 0; row < 2; row++)
                for (int column = 10; column < 20; column++)
                {
                    int offset = row * 32 + column;
                    FailIf(map[offset] != rom.Tile(offset),
                        $"HUD max=${maximum:x2} health=${health:x2} flags=${flags:x2}: tile ${offset:x2} ${map[offset]:x2} != ROM ${rom.Tile(offset):x2}.");
                }
            }
            finally { hud.Free(); }
        }
        foreach (bool batched in new[] { false, true })
        {
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay();
            LoadValidationRoom(0, 0x45);
            _inventoryMenu.OpenImmediatelyForValidation();
            var rom = new MenuRom(_saveData, _currentRoom);
            var sounds = _sound.AttachPlayRequestAudit();
            int frame = 0;
            void Step(int updates)
            {
                application.Step(updates, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    frame = _saveData.ReadWramByte(0xc622);
                    rom.UpdateHud(frame);
                    int money = (rom[0xcbe6] & 15) * 100 + (rom[0xcbe5] >> 4) * 10 + (rom[0xcbe5] & 15);
                    FailIf(_statusBar.DisplayedRupees != money || _statusBar.DisplayedHealth != rom[0xcbe4],
                        $"HUD update {frame}: animated health/money {_statusBar.DisplayedHealth}/{_statusBar.DisplayedRupees} != ROM {rom[0xcbe4]}/{money}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"HUD update {frame}: gain-heart/rupee sound order differs from ROM.");
                });
            }
            _inventory.AddRupees(12);
            _inventory.ApplyDamage(7);
            rom.CopySave(_saveData);
            Step(12);
            _inventory.Heal(7);
            _inventory.AddRupees(-10);
            rom.CopySave(_saveData);
            Step(29);
            _inventory.ApplyDamage(4);
            rom.CopySave(_saveData);
            Step(4);
            _inventoryMenu.CloseImmediatelyForValidation();
            _mapMenu.OpenImmediatelyForValidation();
            _inventory.Heal(2);
            rom.CopySave(_saveData);
            int frozen = _statusBar.DisplayedHealth;
            application.Step(7, Vector2.Zero, batched: batched, afterUpdate: () =>
                FailIf(_statusBar.DisplayedHealth != frozen,
                    "Map menu must suspend HUD updates while global playtime advances."));
            _mapMenu.CloseImmediatelyForValidation();
            _inventoryMenu.OpenImmediatelyForValidation();
            sounds.Clear();
            rom.ClearSounds();
            Step(8); // Resume on global phase, after seven frozen updates.
            _hud.HideStatusBar();
            rom[0xcbe7] = 0xff;
            _inventory.AddRupees(3);
            rom.CopySave(_saveData);
            Step(5); // wDontUpdateStatusBar preserves displayed values.
            _hud.ShowStatusBar();
            rom[0xcbe7] = 0;
            Step(5);
            _inventoryMenu.CloseImmediatelyForValidation();
        }
        GD.Print("Validated clean-US HUD heart tiles at every quarter-health boundary across six capacities, rupee/key/large-indoor fields and animated BCD money/health/sound order in split/batched updates.");
    }
}
