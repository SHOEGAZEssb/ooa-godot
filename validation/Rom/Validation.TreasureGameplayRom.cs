using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTreasureGameplayRom()
    {
        int hostCase1 = 0;
        foreach (bool cancel in new[] { false, true })
        foreach (string name in new[] { "TREASURE_OBJECT_SEED_SATCHEL_00", "TREASURE_OBJECT_HEART_CONTAINER_00", "TREASURE_OBJECT_BOMBS_00", "TREASURE_OBJECT_RUPEES_05" })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x60);
            _entities.Clear();
            _player.WarpTo(new(112, 72), recordSafe: false);
            var treasure = _treasures.GetObject(name);
            var visual = _treasures.GetObjectVisual(treasure.Graphic);
            var record = new GroundTreasureDatabaseRecord(0, 0x60, 0, 72, 80,
                name, visual.Sprite, visual.TileBase, visual.Palette, visual.Animation,
                treasure.TextId, treasure.Message, "ROM shared treasure grant fixture",
                RoomFlagTiming: GroundTreasureRoomFlagTiming.Never);
            var pickup = _entities.Spawn<GroundTreasurePickup>(new GroundTreasureSpawn(record));
            var rom = new TreasureRom();
            rom.Seed(_saveData);
            bool granted = false;
            int transactions = 0;
            void Changed() => transactions++;
            _inventory.Changed += Changed;
            void Compare()
            {
                // Native state2 tests the current Link high bytes before
                // gotoState3 grants. Approach from the positive-X side.
                if (!granted && Math.Floor(_player.Position.X) - 80 < 12)
                {
                    rom.Give(treasure.TreasureId, treasure.Parameter);
                    granted = true;
                }
                // Gameplay also advances playtime/HUD/map state. Compare all
                // inventory, ownership, dungeon and Gasha/rupee-counter bytes.
                for (int address = 0xc672; address <= 0xc6cb; address++)
                    FailIf(_saveData.ReadWramByte(address) != rom[address],
                        $"ROM gameplay treasure {name}, batch={batched}, cancel={cancel}, granted={granted}, WRAM=${address:x4}.");
                foreach (int address in new[] { 0xc627, 0xc628, 0xc65f, 0xc660 })
                    FailIf(_saveData.ReadWramByte(address) != rom[address], $"ROM pickup counter ${address:x4}, {name}.");
                FailIf(transactions != (granted ? 1 : 0), $"Treasure {name} must publish exactly one complete grant.");
            }
            try
            {
                StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
                for (int step = 0; !granted && step < 32; step++)
                {
                    StepGameplayUpdates(2, Vector2.Left, batched: batched, afterUpdate: Compare);
                    FailIf(_currentRoom.IsSolid(_player.Position), "Treasure approach crossed solid room geometry.");
                }
                FailIf(!granted || !_dialogue.IsOpen, $"Treasure {name} failed to grant/open its text on contact.");
                StepGameplayUpdates(5, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(!pickup.Held || !_player.IsHoldingItemTwoHands, "Ground treasure did not reach its held-item pose.");
                if (cancel)
                {
                    _dialogue.Close();
                    LoadValidationRoom(0, 0x66);
                    rom.RoomLoadMaturity(); // loadScreen's separate +5 Gasha credit.
                }
                else _dialogue.Close();
                StepGameplayUpdates(3, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(_player.IsHoldingItemTwoHands || _player.CutsceneControlled,
                    "Treasure completion/cancellation retained Link's item hold.");
                if (!cancel)
                    StepGameplayUpdates(8, Vector2.Left, batched: batched, afterUpdate: Compare);
                FailIf(transactions != 1, "Completed treasure granted again after Link resumed movement.");
            }
            finally { _inventory.Changed -= Changed; }
        }
        GD.Print("Validated shared pickup grants against ROM state through actual approach, held dialogue, completion/cancellation and resumed gameplay, with individual/batched updates and single transactions.");
    }
}
