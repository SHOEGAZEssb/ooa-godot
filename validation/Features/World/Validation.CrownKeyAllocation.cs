using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyAllocation()
    {
        foreach (bool block in new[] { false, true })
        foreach (int free in new[] { 0, 1, 2 })
        {
            int room = block ? 0xa8 : 0xb3;
            byte flag = block ? (byte)0x80 : (byte)2;
            _saveData.SetRoomFlag(4, room, flag, false);
            LoadValidationRoom(4, room);
            _entities.Clear();
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            for (int i = 0; i < 14 - free; i++)
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24, 24), SoundId.MusNone));
            Vector2 center = block ? new(136, 40) : new(232, 136);
            Vector2I direction = block ? Vector2I.Left : Vector2I.Right;
            Vector2 link = center - (Vector2)direction * 12;
            FailIf(_currentRoom.IsSolid(link), "Key allocation fixture requires valid adjacent floor.");
            _player.WarpTo(link);
            _sound.ClearPlayRequestAudit();
            // Isolate the native allocation boundary before object dispatch.
            // CrownKeyLocks separately exercises normal movement and approach.
            for (int i = 0; i < (block ? 20 : 10); i++)
                _keyDoors.UpdatePushAttempt(link, direction, direction);
            var keys = _entities.Entities<DungeonKeyUseEffect>();
            FailIf(keys.Count != (free > 0 ? 1 : 0) ||
                _entities.Entities<PuzzlePuffEffect>().Count != 14 - free + (block && free == 2 ? 1 : 0) ||
                _inventory.GetDungeonSmallKeys(5) != 0 || !_saveData.HasRoomFlag(4, room, flag) ||
                _currentRoom.GetMetatile(center) != (block ? 0xa0 : 0x71) ||
                _sound.PlayRequestsFor(SoundId.SndGetSeed) != 0,
                $"Crown key allocation with{free} free slots must preserve key-first/puff-second order without blocking unlock.");
            if (keys.Count != 0)
                FailIf(_entities.InteractionSlot(keys.Single()) != 16 - free,
                    "Key sprite must occupy the first free native INTERACTION slot.");
            _keyDoors.Advance(6.0 / 60);
            _entities.Update(30.0 / 60, _player);
            FailIf(_entities.Entities<DungeonKeyUseEffect>().Count != 0 ||
                _sound.PlayRequestsFor(SoundId.SndGetSeed) != (free > 0 ? 1 : 0) ||
                _entities.Entities<PuzzlePuffEffect>().Count != 0 || !_entities.InteractionSlotAvailable ||
                _inventory.GetDungeonSmallKeys(5) != 0 || _currentRoom.IsSolid(center),
                "Effect retirement must free slots without retrying skipped key/puff allocations or reverting the lock.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
