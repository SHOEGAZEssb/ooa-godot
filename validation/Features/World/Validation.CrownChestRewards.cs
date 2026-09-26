using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownChestRewards()
    {
        // chestData.s: $9b/$54 boss key31:03, $a5/$53 Cane04:00,
        // $ad/$8b compass32:02, $be/$17 map33:02.
        foreach (var test in new[]
        {
            (Room: 0x9b, Position: 0x54, Treasure: 0x31),
            (Room: 0xa5, Position: 0x53, Treasure: 0x04),
            (Room: 0xad, Position: 0x8b, Treasure: 0x32),
            (Room: 0xbe, Position: 0x17, Treasure: 0x33)
        })
        {
            _saveData.SetRoomFlag(4, test.Room, OracleSaveData.RoomFlagItem, false);
            LoadValidationRoom(4, test.Room);
            Vector2 point = new((test.Position & 15) * 16 + 8, (test.Position >> 4) * 16 + 8);
            // Isolate the reward consumer after puzzle completion. Pattern
            // creation is checked separately by CrownPatternChests.
            if (test.Room is 0x9b or 0xa5)
                _currentRoom.SetPositionTileAndCollision(point, 0xf1, null, (long)_animationTicks);
            FailIf(_currentRoom.GetMetatile(point) != 0xf1 || _rooms.CurrentDungeonIndex != 5,
                "Crown reward must use its source chest and dungeon index5.");
            _player.WarpTo(point + Vector2.Down * 16);
            FailIf(_currentRoom.IsSolid(_player.Position), "Chest approach must start on actual south-side floor.");
            StepGameplayUpdates(4, Vector2.Up, [], [], false);
            FailIf(_currentRoom.IsSolid(_player.Position), "Chest approach must not enter solid geometry.");
            bool Owns(int dungeon) => test.Treasure switch
            {
                0x31 => _inventory.HasDungeonBossKey(dungeon),
                0x32 => _inventory.HasDungeonCompass(dungeon),
                0x33 => _inventory.HasDungeonMap(dungeon),
                _ => _inventory.HasTreasure(TreasureId.CaneOfSomaria)
            };
            bool otherDungeon = Owns(4);
            int keys = _inventory.GetDungeonSmallKeys(5);
            FailIf(Owns(5), $"Crown reward${test.Treasure:x2} fixture must begin unowned.");
            FailIf(!TryInteract(_player) || !_interactions.ChestRewardActive,
                $"Crown4:{test.Room:x2}/${test.Position:x2} chest must open from reachable floor.");
            Vector2 heldPosition = _player.Position;
            int pushCounter = _keyDoors.RemainingPushFrames;
            // treasure.s spawnMode3 retains disabledObjects=$83 until the
            // interaction deletes after text. Link's gate precedes its tile
            // handler, so reserved0 cannot contend with a key-door attempt.
            StepGameplayUpdates(31, Vector2.Down, [], [], true);
            FailIf(Owns(5) || _player.Position != heldPosition ||
                _keyDoors.RemainingPushFrames != pushCounter,
                "Chest rise must retain both reward ownership and Link's tile-input gate through update31.");
            StepGameplayUpdates(1, Vector2.Down, [], [], false);
            FailIf(!Owns(5) || !_dialogue.IsOpen || !_saveData.HasRoomFlag(4, test.Room, OracleSaveData.RoomFlagItem) ||
                _inventory.GetDungeonSmallKeys(5) != keys || test.Treasure != 0x04 && Owns(4) != otherDungeon,
                $"Crown reward${test.Treasure:x2} must grant exactly its inventory entry and preserve other dungeon data.");
            StepGameplayUpdates(10, Vector2.Down, [], [], true);
            FailIf(_player.Position != heldPosition || !_interactions.ChestRewardActive ||
                _keyDoors.RemainingPushFrames != pushCounter,
                "Chest dialogue must retain the reward and block movement/tile dispatch.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Down, [], [], false);
            FailIf(_interactions.ChestRewardActive || _player.Position != heldPosition ||
                _keyDoors.RemainingPushFrames != pushCounter,
                "The cleanup update must delete the reserved reward after Link's blocked update.");
            FailIf(TryInteract(_player), "An opened Crown chest must not grant its reward twice.");
            StepGameplayUpdates(1, Vector2.Down, [], [], false);
            FailIf(_player.Position.Y <= heldPosition.Y || _inventory.GetDungeonSmallKeys(5) != keys,
                "Link must resume normal input on the update after chest cleanup without consuming a key.");
            LoadValidationRoom(0, 0x60);
            LoadValidationRoom(4, test.Room);
            FailIf(!Owns(5) || _currentRoom.GetMetatile(point) != 0xf0,
                "Crown chest ownership and its opened tile must persist on re-entry.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
