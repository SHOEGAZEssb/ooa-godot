using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateItemUseGameplayRom()
    {
        var rom = new ItemUseRom();
        void Room(int a, int b, int bombs = 0x10)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            foreach (int item in new[] { TreasureId.Sword, TreasureId.Shovel, TreasureId.CaneOfSomaria, TreasureId.Boomerang })
                _inventory.GiveTreasure(item, 1);
            _inventory.GiveTreasure(TreasureId.Bombs, bombs);
            _inventory.EquipA(a); _inventory.EquipB(b);
            _player.WarpTo(new(72, 80)); _player.Face(Vector2I.Right);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
        }
        int hostCase4 = 0;
        foreach (int other in new[] { TreasureId.Shovel, TreasureId.Bombs, TreasureId.CaneOfSomaria, TreasureId.Boomerang })
        foreach (bool swordA in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase4++))
        {
            int a = swordA ? TreasureId.Sword : other;
            int b = swordA ? other : TreasureId.Sword;
            Room(a, b);
            int bombs = _inventory.Bombs;
            rom.Reset([]); rom.Allocate(a, b, 3, 3);
            StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"], batched);
            FailIf(_player.IsAttacking != (rom[0xd201] == TreasureId.Sword) ||
                _player.IsUsingShovel || _player.IsUsingSomaria || _playerWorld.BombParentActive ||
                _player.IsUsingBoomerang != (rom[0xd301] == TreasureId.Boomerang) || _inventory.Bombs != bombs,
                $"ROM simultaneous A=${a:x2}, B=${b:x2}, batch={batched}: parent2=${rom[0xd201]:x2}, sword={_player.IsAttacking}, shovel={_player.IsUsingShovel}, bomb={_playerWorld.BombParentActive}, ammo={_inventory.Bombs:x2}/{bombs:x2}.");
        }
        int hostCase3 = 0;
        foreach (bool bombA in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            int a = bombA ? TreasureId.Bombs : TreasureId.CaneOfSomaria;
            int b = bombA ? TreasureId.CaneOfSomaria : TreasureId.Bombs;
            Room(a, b, bombs: 0);
            FailIf(_inventory.Bombs != 0, "The empty-ammo fixture must start without bombs.");
            rom.Reset([]); rom[0xcc2c] = 0xd0;
            rom.Allocate(a, b, 3, 3);
            FailIf(rom[0xd201] != TreasureId.Bombs, "Bombs must reserve parent2 before checking ammo.");
            rom.UpdateParents();
            StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"], batched);
            FailIf(rom[0xd200] != 0 || _playerWorld.BombParentActive || _player.IsUsingSomaria || _inventory.Bombs != rom[0xc6b0],
                "An empty bomb parent must clear after allocation; a displaced/rejected cane cannot run as fallback.");
        }
        int hostCase2 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int item in new[] { TreasureId.Bombs, TreasureId.Boomerang })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            Room(primary ? item : 0, primary ? 0 : item);
            string button = primary ? "attack" : "item";
            int bombs = _inventory.Bombs;
            rom.Reset([]); rom[0xc6b0] = (byte)bombs; rom[0xcc2c] = 0xd0;
            rom[0xd008] = 1;
            for (int i = 0; i < 5; i++)
            {
                _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(new(160, 112), ObjectDirection.Right));
                rom[0xd700 + i * 0x100] = 1; rom[0xd701 + i * 0x100] = 0x0b;
            }
            rom.Allocate(primary ? item : 0, primary ? 0 : item, primary ? 1 : 2, primary ? 1 : 2);
            rom.UpdateParents();
            StepGameplayUpdates(1, Vector2.Zero, [button], [button], batched);
            FailIf(rom[0xd200] != 0 || rom[0xd300] != 0 || _playerWorld.BombParentActive ||
                _player.IsUsingBoomerang || _inventory.Bombs != rom[0xc6b0] || _inventory.Bombs != bombs,
                $"ROM ITEM${item:x2} full child pool must clear its parent without consuming ammo.");
            _entities.ClearPhysicalPlayerItems();
            StepGameplayUpdates(3, Vector2.Zero, [button], [], batched);
            FailIf(_playerWorld.BombParentActive || _player.IsUsingBoomerang || _inventory.Bombs != bombs,
                "A failed allocation must not retry on held input after the child pool becomes free.");
            StepGameplayUpdates(1, Vector2.Zero);
            StepGameplayUpdates(1, Vector2.Zero, [button], [button], batched);
            FailIf(item == TreasureId.Bombs ? !_playerWorld.BombParentActive || _inventory.Bombs != 0x09 : !_player.IsUsingBoomerang,
                "Releasing and pressing again must permit a fresh allocation and spend exactly one bomb when applicable.");
        }
        int hostCase1 = 0;
        foreach (string state in new[] { "ground", "air", "hurt", "carry", "swim", "dialogue" })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            Room(TreasureId.Sword, TreasureId.Bombs);
            rom.Reset([]);
            switch (state)
            {
                case "air":
                    _inventory.GiveTreasure(TreasureId.Feather, 0);
                    _inventory.EquipB(TreasureId.Feather);
                    StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
                    FailIf(_player.IsGroundedForFloorButton, "The item gate fixture must be in a real feather jump.");
                    rom[0xcc5c] = 1; // Ordinary airborne state, not bit7's prohibition.
                    break;
                case "hurt":
                    FailIf(!_player.ApplyEnemyContactDamage(_player.Position - new Vector2(24, 0), 1),
                        "The item gate fixture must accept a real contact hit.");
                    rom[0xd02d] = (byte)_player.KnockbackFrames;
                    rom[0xd02b] = (byte)_player.InvincibilityFrames;
                    break;
                case "carry":
                    StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batched);
                    StepGameplayUpdates(20, Vector2.Zero, ["item"], [], batched);
                    FailIf(!_player.IsCarryingObject, "The item gate fixture must finish lifting its bomb.");
                    rom[0xcc5a] = 1;
                    break;
                case "swim":
                    LoadValidationRoom(0, 0x33); _entities.Clear();
                    _inventory.GiveTreasure(TreasureId.Flippers, 0);
                    for (int y = 8; y < 128; y += 16)
                    for (int x = 8; x < 160; x += 16)
                        _currentRoom.SetPositionTileAndCollision(new(x, y), 0xfa, 0x10, 0);
                    _player.WarpTo(new(80, 64));
                    StepGameplayUpdates(16, Vector2.Zero, batched: batched);
                    FailIf(!_player.TopDownSwimming, "The item gate fixture must enter swimming through the gameplay loop.");
                    rom[0xcc5d] = (byte)_player.TopDownSwimmingState;
                    break;
                case "dialogue":
                    _dialogue.ShowMessage("Item dispatch pause.", _player.Position.Y);
                    break;
            }
            // Dialogue freezes Link before checkUseItems. Do not manufacture
            // a native checkUseItems call that the original caller cannot make.
            if (state != "dialogue") rom.Allocate(TreasureId.Sword, 0, 1, 1);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            FailIf(_player.IsAttacking != (rom[0xd201] == TreasureId.Sword),
                $"ROM shared item gate state={state}, batch={batched}: native sword={rom[0xd201] == TreasureId.Sword}, runtime={_player.IsAttacking}.");
            if (state == "hurt")
            {
                rom[0xcc2c] = 0xd0;
                rom.UpdateParents();
                int updates = 0;
                StepGameplayUpdates(20, Vector2.Zero, ["attack"], [], batched, afterUpdate: () =>
                {
                    rom.Allocate(TreasureId.Sword, 0, 1, 0);
                    rom.UpdateParents();
                    FailIf(_player.IsAttacking != (rom[0xd200] != 0),
                        $"ROM item during/after recoil update={++updates}, batch={batched}: sword parent lifetime differs.");
                });
                FailIf(_player.KnockbackFrames != 0, "The item/recoil fixture must run past knockback completion.");
            }
        }
        GD.Print("Validated ROM A/B priority, shared gameplay gates, failed child allocation, ammo preservation, held input and release/repress with individual and batched updates.");
    }
}
