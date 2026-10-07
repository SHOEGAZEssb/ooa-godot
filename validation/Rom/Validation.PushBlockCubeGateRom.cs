using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ComparePushBlockCubeGateRom()
    {
        int fixture = 0;
        foreach (var (room,position,color) in new[] {
            (0x9b,0,0), (0x9b,0x36,2), (0x9b,0x36,0x81), (0x9b,0x36,0x82),
            (0x09,0x36,0x82) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,room); _entities.Clear();
            var declared = new DeclaredColoredCubeState(_runtimeState); _entities.AddEntity(declared);
            var cube = declared.ColoredCubePuzzleState;
            cube.CubePosition = position; cube.CubeColor = color;
            bool blue = room == 0x9b;
            int direction = blue ? 16 : 8;
            Vector2 start = blue ? new(88.25f,40.5f) : new(184.25f,56.5f);
            Vector2 source = blue ? new(88,56) : new(200,56);
            Vector2 destination = blue ? new(88,72) : new(216,56);
            byte tile = blue ? (byte)0x2e : (byte)0x19;
            _player.WarpTo(start); _player.Face(blue ? Vector2I.Down : Vector2I.Right);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(source) != tile ||
                _currentRoom.GetMetatile(destination) != 0xa0,
                $"Cube gate must approach original room$4:${room:x2} tile${tile:x2} through source floor geometry.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,blue ? 2 : 1,(int)start.X,(int)start.Y);
            rom.Word(0xd00a,(int)(start.Y * 256)); rom.Word(0xd00c,(int)(start.X * 256));
            rom[0xccae] = (byte)position; rom[0xccad] = (byte)color;
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() =>
                {
                    string context = $"Push cube pos${position:x2}/color${color:x2}, batch={batch}, update={++update}";
                    FailIf(_pushBlocks.RemainingPushFrames != rom[0xcc6a] ||
                        _pushBlocks.Active != (rom[0xd140] != 0) || _rooms.BlockPushAngle != rom[0xcca6],
                        context + $": contact clock/allocation/angle differ: runtime={_pushBlocks.RemainingPushFrames}/{_pushBlocks.Active}/${_rooms.BlockPushAngle:x2}, native={rom[0xcc6a]}/{rom[0xd140] != 0}/${rom[0xcca6]:x2}.");
                    if (_pushBlocks.Active)
                        FailIf(_pushBlocks.BlockTopLeft + new Vector2(8,6) !=
                            new Vector2(rom.Word(0xd14c) / 256f,rom.Word(0xd14a) / 256f) ||
                            _pushBlocks.ActiveMoveFrames - (int)SomariaPrivate<float>(_pushBlocks,"_moveFrame") != rom[0xd146],
                            context + ": full fixed block position/movement counter differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                        context + ": ordered cues/shared RNG differ.");
                });
            bool permitted = position == 0 || blue && color == 0x82;
            Step();
            // Reach contact through native collision geometry, then observe
            // before/during/exact expiry rather than directly stepping $14.
            for (int wait = 0; _pushBlocks.RemainingPushFrames > 10 && wait < 40; wait++) Step(angle:direction);
            FailIf(_pushBlocks.RemainingPushFrames != 10,"Cube permission must not bypass the contact countdown.");
            _dialogue.ShowMessage("Cube-gate pause.",_player.Position.Y); rom[0xcba0] = 1;
            Step(3,direction); _dialogue.Close(); rom[0xcba0] = 0;
            for (int wait = 0; _pushBlocks.RemainingPushFrames != 1 && wait < 40; wait++) Step(angle:direction);
            FailIf(_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 1,
                $"Cube-gated block must not allocate before contact update20: pos${position:x2}/color${color:x2}, counter={_pushBlocks.RemainingPushFrames}, active={_pushBlocks.Active}.");
            Step(angle:direction);
            FailIf(_pushBlocks.Active != permitted || sounds.Requests.Count != (permitted ? 1 : 0) ||
                !permitted && (_rooms.BlockPushAngle != 0 || _currentRoom.GetMetatile(source) != tile),
                "Native cube rejection must delete reserved$14 without tile, angle or movement sound side effects.");
            if (!permitted)
            {
                Step(10,direction);
                // Changing the caller's shared input halfway through a retry
                // must not restart contact or be latched before state0.
                if (blue) { cube.CubeColor = 0x82; rom[0xccad] = 0x82; }
                else { cube.CubePosition = 0; rom[0xccae] = 0; }
                Step(9,direction);
                FailIf(_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 1,
                    "Mid-contact cube permission must retain the existing clock.");
                Step(angle:direction);
                FailIf(!_pushBlocks.Active,"Newly permitted cube input must initialize the held retry.");
            }
            Step(31);
            FailIf(_pushBlocks.Active || _currentRoom.GetMetatile(source) != 0xa0 ||
                _currentRoom.GetMetatile(destination) != (blue ? 0x2e : 0x1d) || sounds.Requests.Count != 1,
                "Permitted block must complete exactly32 updates and replace source/destination once.");
            Step(40,direction);
            FailIf(_pushBlocks.Active || sounds.Requests.Count != 1,
                "Repeated contact must respect the original obstruction or stationary destination tile.");
        }
    }
}
