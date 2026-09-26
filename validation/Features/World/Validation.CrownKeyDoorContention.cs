using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorContention()
    {
        foreach (bool batch in new[] { false, true })
        {
            _saveData.SetRoomFlag(4, 0xb3, 2, false);
            LoadValidationRoom(4, 0xb3);
            _entities.Clear();
            _player.WarpTo(new(220, 136));
            _player.Face(Vector2I.Right);
            FailIf(_currentRoom.IsSolid(_player.Position), "Key-door contention starts on actual adjacent floor.");
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            for (int i = 0; i < 2; i++)
                _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            var oldPalette = _entities.PaletteFadeActiveSource;
            _entities.PaletteFadeActiveSource = () => true;
            void Step(int count, Vector2 movement) => StepGameplayUpdates(count, movement, batched: batch);
            _sound.ClearPlayRequestAudit();
            try
            {
                // Hold just the palette signal: it gates door state2, not Link's
                // tile dispatcher. This isolates nextToKeyDoor's reserved0 test.
                for (int i = 0; !_keyDoors.Opening && i < 30; i++) Step(1, Vector2.Right);
                FailIf(!_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 1 ||
                    _keyDoors.RemainingPushFrames != 20 || _currentRoom.GetMetatile(new(232, 136)) != 0x71,
                    "First key debit must allocate the reserved opener while palette fade holds the closed tile.");
                Step(9, Vector2.Right);
                FailIf(_inventory.GetDungeonSmallKeys(5) != 1 || _keyDoors.RemainingPushFrames != 2,
                    "A busy reserved opener must not bypass the next ten-update push countdown.");
                Step(1, Vector2.Right);
                FailIf(_inventory.GetDungeonSmallKeys(5) != 0 || !_keyDoors.Opening ||
                    _keyDoors.RemainingPushFrames != 20 || _keyDoors.OpeningCounter != 0 ||
                    _entities.Entities<DungeonKeyUseEffect>().Count != 1 ||
                    _sound.PlayRequestsFor(SoundId.SndGetSeed) != 1,
                    "Reserved0 contention must debit before rejecting allocation, with no second key sprite or restarted opener.");
                Step(10, Vector2.Right);
                FailIf(!_dialogue.IsOpen || !_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 0,
                    "A later push without a key must reach the missing-key message even while reserved0 is busy.");
                _dialogue.Close();
                _entities.PaletteFadeActiveSource = oldPalette;
                Step(7, Vector2.Zero);
                FailIf(_keyDoors.Opening || _currentRoom.IsSolid(new(232, 136)) ||
                    !_saveData.HasRoomFlag(4, 0xb3, 2),
                    "Releasing the fade must finish the original opener and preserve its room flag.");
            }
            finally
            {
                _dialogue.Close();
                _entities.PaletteFadeActiveSource = oldPalette;
                LoadValidationRoom(0, 0x60);
            }
        }
    }
}
