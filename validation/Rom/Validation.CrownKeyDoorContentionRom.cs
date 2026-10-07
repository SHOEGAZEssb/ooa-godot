using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorContention()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4, 0xb3, 2, false);
            LoadValidationRoom(4, 0xb3);
            _entities.Clear();
            _player.WarpTo(new(220, 136));
            _player.Face(Vector2I.Right);
            FailIf(_currentRoom.IsSolid(_player.Position), "Key-door contention starts on actual adjacent floor.");
            while (_inventory.TryUseDungeonSmallKey(5)) { }
            for (int i = 0; i < 2; i++)
                _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            var freeze = new CrownEntranceFreeze();
            _entities.AddEntity(freeze);
            var oldScreen = _entities.WorldToScreen;
            _entities.WorldToScreen = position => position - new Vector2(80,48);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,220,136);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom.CreateMenuView().LoadDungeon(5);
            rom[0xffaa] = 48; rom[0xffac] = 80;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count, Vector2 movement)
            {
                rom[0xcc8a] = freeze.FreezesRoomEntities ? (byte)0x02 : (byte)0;
                int angle = movement == Vector2.Zero ? 0xff : 8;
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() =>
                {
                    string context = $"Key-door contention batch={batch}, update={++update}";
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,context,excludeText:true);
                    FailIf(_keyDoors.RemainingPushFrames != rom[0xcc6a] ||
                        _rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                        context + $": shared contact clock/hint/modal state differs: runtime={_keyDoors.RemainingPushFrames}/${_rooms.InformativeTextsShown:x2}/{_dialogue.IsOpen}, native={rom[0xcc6a]}/${rom[0xccd7]:x2}/{rom[0xcba0] != 0}.");
                    for (int room = 0; room < 256; room++)
                        FailIf(_saveData.GetRoomFlags(4,room) != rom[0xc900 + room],
                            context + $": room flags$4:${room:x2} differ.");
                });
            }
            try
            {
                Step(1,Vector2.Zero); // Publish current room walls/input before held contact.
                // Freeze initialized interaction handlers while Link remains
                // eligible, as DISABLE_INTERACTIONS $02 does. Palette would
                // also freeze Link and cannot establish reserved0 contention.
                for (int i = 0; !_keyDoors.Opening && i < 30; i++) Step(1, Vector2.Right);
                FailIf(!_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 1 ||
                    _keyDoors.RemainingPushFrames != 20 || _currentRoom.GetMetatile(new(232, 136)) != 0x71,
                    "First key debit must allocate the reserved opener while the interaction mask holds the closed tile.");
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
                FailIf(rom.TextGeneration != 1 || rom.Word(0xcba2) != 0x5500 || _rooms.InformativeTextsShown != 4,
                    "Busy reserved0 must still publish source TX_5100/mask$04 after its second key debit.");
                Step(3,Vector2.Right);
                _dialogue.Close();
                rom[0xcba0] = 0;
                freeze.FreezesRoomEntities = false;
                Step(7, Vector2.Zero);
                FailIf(_keyDoors.Opening || _currentRoom.IsSolid(new(232, 136)) ||
                    !_saveData.HasRoomFlag(4, 0xb3, 2),
                    "Releasing the fade must finish the original opener and preserve its room flag.");
                Step(32,Vector2.Zero); Step(4,Vector2.Right);
                FailIf(_keyDoors.Opening || _entities.Entities<DungeonKeyUseEffect>().Count != 0 || rom.TextGeneration != 1,
                    "Completed opener/key lifetime and repeated input must not allocate or reopen a missing-key hint.");
            }
            finally
            {
                _dialogue.Close();
                freeze.FreezesRoomEntities = false;
                _entities.WorldToScreen = oldScreen;
                LoadValidationRoom(0, 0x60);
            }
        }
    }
}
