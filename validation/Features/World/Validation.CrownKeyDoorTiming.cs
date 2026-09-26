using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorTiming()
    {
        foreach (bool batch in new[] { false, true })
        foreach (string mode in new[] { "normal", "text", "freeze", "palette" })
        {
            _saveData.SetRoomFlag(4, 0xb3, 2, false);
            LoadValidationRoom(4, 0xb3);
            _entities.Clear();
            Vector2 center = new(232, 136);
            _player.WarpTo(new(220, 136));
            FailIf(_currentRoom.IsSolid(_player.Position), "Key-door timing requires actual adjacent floor.");
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            for (int i = 0; i < 10; i++)
                _keyDoors.UpdatePushAttempt(_player.Position, Vector2I.Right, Vector2.Right);
            var freeze = new CrownEntranceFreeze();
            typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_entities, [freeze]);
            var oldPalette = _entities.PaletteFadeActiveSource;
            var oldScreen = _entities.WorldToScreen;
            // Isolate timing with the viewport at this large room's lower-right
            // corner. Screen-boundary behavior is checked by CrownKeyDoorEdges.
            _entities.WorldToScreen = position => position - new Vector2(80, 48);
            bool palette = mode == "palette";
            _entities.PaletteFadeActiveSource = () => palette;
            void Step(int n) => StepGameplayUpdates(n, Vector2.Zero, batched: batch);
            void Hold()
            {
                if (mode == "text") _dialogue.ShowMessage("Key door hold.", 120);
                freeze.FreezesRoomEntities = mode == "freeze";
                palette = mode == "palette";
            }
            void Release()
            {
                _dialogue.Close();
                freeze.FreezesRoomEntities = false;
                palette = false;
            }
            _sound.ClearPlayRequestAudit();
            try
            {
                Hold();
                Step(1);
                FailIf(_currentRoom.GetMetatile(center) != 0x71 || _keyDoors.OpeningCounter != 0 ||
                    _sound.PlayRequestsFor(SoundId.SndDoorClose) != 0,
                    "Reserved INTERAC$1e:$00 state0 must yield before animating the key door.");
                if (mode != "normal")
                {
                    Step(3);
                    FailIf(_currentRoom.GetMetatile(center) != 0x71 || _keyDoors.OpeningCounter != 0,
                        $"Key door must retain its closed tile during {mode}.");
                }
                Release();
                if (mode == "text")
                {
                    // Text blocked incstate even though state0 initialization ran.
                    Step(1);
                    FailIf(_currentRoom.GetMetatile(center) != 0x71,
                        "Releasing text must first resume incstate, which yields before animation.");
                }
                Step(1);
                FailIf(_currentRoom.GetMetatile(center) != 0xa0 || !_currentRoom.IsSolid(center) ||
                    _keyDoors.OpeningCounter != 6 || _sound.PlayRequestsFor(SoundId.SndDoorClose) != 1,
                    $"Key-door start failed ({mode}, batch={batch}): tile={_currentRoom.GetMetatile(center):x2}, " +
                    $"counter={_keyDoors.OpeningCounter}, screen={_entities.WorldToScreen(center)}, " +
                    $"sound={_sound.PlayRequestsFor(SoundId.SndDoorClose)}.");
                Hold();
                if (mode != "normal")
                {
                    Step(3);
                    FailIf(_keyDoors.OpeningCounter != 6 || !_currentRoom.IsSolid(center),
                        $"Key-door interleave timer must stop during {mode}.");
                }
                Release();
                Step(5);
                FailIf(!_keyDoors.Opening || _keyDoors.OpeningCounter != 1 || !_currentRoom.IsSolid(center),
                    "Key door must remain solid through five interleave updates.");
                Step(1);
                FailIf(_keyDoors.Opening || _currentRoom.IsSolid(center) ||
                    _sound.PlayRequestsFor(SoundId.SndDoorClose) != 2 ||
                    _inventory.GetDungeonSmallKeys(5) != 0,
                    "The sixth interleave update must open collision, play sound and finish without a second key debit.");
            }
            finally
            {
                Release();
                _entities.PaletteFadeActiveSource = oldPalette;
                _entities.WorldToScreen = oldScreen;
                LoadValidationRoom(0, 0x60);
            }
        }
    }
}
