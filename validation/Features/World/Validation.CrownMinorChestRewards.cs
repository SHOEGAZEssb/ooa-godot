using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownMinorChestRewards()
    {
        // chestData.s: $99/$19 gives RUPEES_05 (50); $9e/$47,
        // $9f/$57 and $a3/$57 each give SMALL_KEY_03 to dungeon5.
        foreach (var chest in new (int Room, int Position, bool Rupees)[]
            { (0x99,0x19,true), (0x9e,0x47,false), (0x9f,0x57,false), (0xa3,0x57,false) })
        {
            // Isolate the $9f reward with the source's lowered blue-floor
            // state. Room-load substitutions must provide its real approach.
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, (byte)(chest.Room == 0x9f ? 1 : 0));
            _saveData.SetRoomFlag(4, chest.Room, 0x20, false);
            LoadValidationRoom(4, chest.Room);
            _entities.Clear();
            Vector2 center = new((chest.Position & 15) * 16 + 8, (chest.Position >> 4) * 16 + 8);
            // Stage the pattern chest as already created; this check owns
            // reward consumption, not block placement or room progression.
            if (chest.Room == 0x9e)
            {
                // room049e.bin starts with statues at $37/$57; the source
                // solved pattern puts them at $45/$49 and restores floor.
                foreach (int position in new[] { 0x37, 0x57, 0x45, 0x49 })
                {
                    Vector2 tileCenter = new((position & 15) * 16 + 8, (position >> 4) * 16 + 8);
                    _currentRoom.SetPositionTileAndCollision(tileCenter,
                        (byte)(position is 0x37 or 0x57 ? 0xa0 : 0x2a), null, (long)_animationTicks);
                }
                _currentRoom.SetPositionTileAndCollision(center, 0xf1, null, (long)_animationTicks);
            }
            FailIf(_currentRoom.GetMetatile(center) != 0xf1,
                $"Crown4:{chest.Room:x2}/${chest.Position:x2} must use its source chest tile.");
            _player.WarpTo(center + Vector2.Down * 16);
            FailIf(_collision.Collides(_player.Position), $"Minor Crown4:{chest.Room:x2} chest approach at{_player.Position} must use real south-side floor.");
            StepGameplayUpdates(4, Vector2.Up);
            _inventory.AddRupees(100 - _inventory.Rupees);
            int count() => chest.Rupees ? _inventory.Rupees : _inventory.GetDungeonSmallKeys(5);
            int before = count();
            int otherKeys = _inventory.GetDungeonSmallKeys(4);
            int crownKeys = _inventory.GetDungeonSmallKeys(5);
            FailIf(!TryInteract(_player), "Minor Crown chest must open after an ordinary floor approach.");
            StepGameplayUpdates(31, Vector2.Zero, batched: true);
            FailIf(count() != before, "Minor Crown chest must not grant its reward before the full32-update rise.");
            StepGameplayUpdates(1, Vector2.Zero);
            int expected = before + (chest.Rupees ? 50 : 1);
            FailIf(count() != expected || !_saveData.HasRoomFlag(4, chest.Room, 0x20) ||
                !_dialogue.IsOpen || _inventory.GetDungeonSmallKeys(4) != otherKeys ||
                chest.Rupees && _inventory.GetDungeonSmallKeys(5) != crownKeys ||
                !chest.Rupees && _inventory.Rupees != 100,
                "Crown chest must grant exactly50 rupees or one dungeon5 key, preserve unrelated inventory and set its item flag.");
            _dialogue.Close();
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(TryInteract(_player) || count() != expected, "An opened minor Crown chest must not grant twice.");
            LoadValidationRoom(0, 0x60);
            LoadValidationRoom(4, chest.Room);
            FailIf(_currentRoom.GetMetatile(center) != 0xf0 || count() != expected,
                "Minor Crown chest rewards and opened tiles must persist on re-entry.");
        }
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(0, 0x60);
    }
}
