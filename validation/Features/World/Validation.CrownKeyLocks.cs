using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyLocks()
    {
        // Original room layouts; block$1e uses flag$80, directional doors
        // use low flags and the opposite flag in the dungeon-layout neighbor.
        foreach (var test in new[]
        {
            (Room: 0x9a, Position: 0x50, Tile: 0x73, Direction: Vector2I.Left, Flag: 8),
            (Room: 0xa8, Position: 0x28, Tile: 0x1e, Direction: Vector2I.Left, Flag: 0x80),
            (Room: 0xac, Position: 0x25, Tile: 0x1e, Direction: Vector2I.Up, Flag: 0x80),
            (Room: 0xb3, Position: 0x8e, Tile: 0x71, Direction: Vector2I.Right, Flag: 2),
            (Room: 0xbd, Position: 0x50, Tile: 0x73, Direction: Vector2I.Left, Flag: 8),
            (Room: 0xbe, Position: 0x5e, Tile: 0x75, Direction: Vector2I.Right, Flag: 2)
        })
        {
            bool block = test.Tile == 0x1e, boss = test.Tile == 0x75;
            _saveData.SetRoomFlag(4, test.Room, (byte)test.Flag, false);
            LoadValidationRoom(4, test.Room);
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            int neighbor = -1;
            byte oppositeFlag = test.Direction == Vector2I.Left ? (byte)2 : (byte)8;
            if (!block)
            {
                FailIf(!_rooms.TryGetNeighbor(test.Direction, out neighbor), "Crown key door requires a floor-layout neighbor.");
                _saveData.SetRoomFlag(4, neighbor, oppositeFlag, false);
            }
            Vector2 point = new((test.Position & 15) * 16 + 8, (test.Position >> 4) * 16 + 8);
            FailIf(_currentRoom.GetMetatile(point) != test.Tile, "Crown lock fixture must use its unchanged source tile.");
            _player.WarpTo(point - (Vector2)test.Direction * 32);
            _player.ApplyInteractionInvincibility(240); // Isolate the lock from unrelated enemy damage.
            FailIf(_currentRoom.IsSolid(_player.Position), "Crown lock approach must begin on actual floor.");
            void Step(Vector2 movement = default)
            {
                StepGameplayUpdates(1, movement, [], [], false);
                FailIf(_currentRoom.IsSolid(_player.Position), "Crown lock approach crossed solid geometry.");
            }
            for (int i = 0; !_dialogue.IsOpen && i < 100; i++) Step(test.Direction);
            string expected = block ? "Huh? This block\nhas a keyhole." : boss ? "This keyhole\nis different!" : "You need a key\nfor this door!";
            FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != expected ||
                _currentRoom.GetMetatile(point) != test.Tile || _saveData.HasRoomFlag(4, test.Room, (byte)test.Flag),
                $"Crown4:{test.Room:x2} must reject its missing key without changing the lock.");
            _dialogue.Close();
            Step();
            _inventory.GiveTreasure(_treasures.GetObject(boss ? "TREASURE_OBJECT_BOSS_KEY_03" : "TREASURE_OBJECT_SMALL_KEY_03"));
            int otherKeys = _inventory.GetDungeonSmallKeys(4);
            for (int i = 0; !_saveData.HasRoomFlag(4, test.Room, (byte)test.Flag) && i < 45; i++) Step(test.Direction);
            FailIf(!_saveData.HasRoomFlag(4, test.Room, (byte)test.Flag) || _inventory.GetDungeonSmallKeys(5) != 0 ||
                _inventory.GetDungeonSmallKeys(4) != otherKeys || boss && !_inventory.HasDungeonBossKey(5) ||
                !block && !_saveData.HasRoomFlag(4, neighbor, oppositeFlag),
                "Crown lock must consume one small key or retain its boss key and persist the correct room flags.");
            for (int i = 0; i < 7; i++) Step();
            FailIf(_currentRoom.GetMetatile(point) != 0xa0 || _currentRoom.IsSolid(point),
                "Crown key lock must finish as passable floor.");
            for (int i = 0; i < 8; i++) Step(test.Direction);
            FailIf(_inventory.GetDungeonSmallKeys(5) != 0 || _dialogue.IsOpen,
                "An opened lock must not consume another key or repeat its missing-key message.");
            LoadValidationRoom(0, 0x60);
            LoadValidationRoom(4, test.Room);
            FailIf(_currentRoom.GetMetatile(point) != 0xa0 || _currentRoom.IsSolid(point),
                "Crown key lock must remain open after re-entry.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
