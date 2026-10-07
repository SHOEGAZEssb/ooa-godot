using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorEdges()
    {
        int fixture = 0;
        // objectCheckWithinScreenBoundary uses byte X+7<$af, Y+7<$8f.
        // Collision cases declare another owner's write before state2 starts.
        foreach (var spec in new (Vector2 Point,bool Visible,int Collision)[] {
            (new(-8,64),false,-1),(new(-7,64),true,-1),
            (new(167,64),true,-1),(new(168,64),false,-1),
            (new(80,-8),false,-1),(new(80,-7),true,-1),
            (new(80,135),true,-1),(new(80,136),false,-1),
            (new(80,64),true,0),(new(80,64),true,0x10)})
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xb3); _entities.Clear();
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = new(204.25f,136.5f),center = new(232,136);
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(center) != 0x71,
                "Door edge fixture must approach through original room4:b3 floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,204,136);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom.CreateMenuView().LoadDungeon(5);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() => {
                    string context = $"Door edge{spec.Point}, collision={spec.Collision}, batch={batch}, update={++update}";
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,context);
                    FailIf(_keyDoors.RemainingPushFrames != rom[0xcc6a],context + ": shared contact clock differs.");
                });
            var oldScreen = _entities.WorldToScreen;
            void Camera(Vector2 screenPoint)
            {
                Vector2 offset = center-screenPoint;
                _entities.WorldToScreen = position => position-offset;
                rom[0xffaa] = unchecked((byte)(int)offset.Y);
                rom[0xffac] = unchecked((byte)(int)offset.X);
            }
            try
            {
                Step();
                for (int wait = 0; !_keyDoors.Opening && wait < 64; wait++) Step(angle:8);
                FailIf(!_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 0 ||
                    _currentRoom.GetMetatile(center) != 0x71,
                    "Reachable allocation must debit once and yield before door state2.");
                Camera(spec.Point);
                if (spec.Collision >= 0)
                {
                    _currentRoom.SetPositionTileAndCollision(center,0xa1,(byte)spec.Collision,(long)_animationTicks);
                    rom[0xcf8e] = 0xa1; rom[0xce8e] = (byte)spec.Collision;
                    Step(); Step(8);
                    FailIf(_keyDoors.Opening || _currentRoom.GetMetatile(center) != 0xa1 ||
                        sounds.Requests.Contains(SoundId.SndDoorClose) || _inventory.GetDungeonSmallKeys(5) != 0,
                        "Already-passable/allowHoles collision must skip the opener without a delayed write or key refund.");
                }
                else
                {
                    Step();
                    FailIf(!_keyDoors.Opening || _keyDoors.OpeningCounter != 6 || !_currentRoom.IsSolid(center) ||
                        sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != (spec.Visible ? 1 : 0),
                        "Door start must sample the exact signed viewport edge and retain world collision.");
                    Camera(spec.Visible ? new(168,64) : new(80,64));
                    Step(5);
                    FailIf(_keyDoors.OpeningCounter != 1 || !_currentRoom.IsSolid(center),
                        "Camera change must retain five interleave updates and world collision.");
                    Step();
                    FailIf(_keyDoors.Opening || _currentRoom.IsSolid(center) ||
                        sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 1,
                        "Door completion must resample visibility and open collision on its sixth update.");
                }
                Step(32); Step(4,8); Step(4,24);
                FailIf(_keyDoors.Opening || _entities.Entities<DungeonKeyUseEffect>().Count != 0 ||
                    _inventory.GetDungeonSmallKeys(5) != 0,
                    "Finished/skipped opener must retire its key and reject renewed spending.");
            }
            finally { _entities.WorldToScreen = oldScreen; }
        }
    }
}
