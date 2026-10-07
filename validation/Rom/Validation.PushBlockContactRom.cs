using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePushBlockContactRom()
    {
        var geometry = new LinkCollisionRom();
        int accepted = 0;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            // Original bank06:$433f specialObjectCheckPushingAgainstTile's
            // shared corner gate; fractions do not participate in its checks.
            Vector2 point = new(176+x+255/256f,112+y+0.5f);
            geometry.Word(0xd00a,(int)(point.Y*256));
            geometry.Word(0xd00c,(int)(point.X*256));
            geometry.Call(0x433f,bank:6);
            bool native = (geometry[0xc201] & 0x10) != 0;
            FailIf(InteractableTilePushGeometry.IsAlignedForPush(point) != native,
                $"Native push corner gate differs at low-byte XY=${x:x1},${y:x1}.");
            if (native) accepted++;
        }
        FailIf(accepted != 231,"Native corner gate must reject exactly25 of256 pixel alignments.");

        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x09); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(24.25f,24.5f)); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.Layout.Length != 0xb0 ||
                _currentRoom.Layout[0x79] != 0xa0,
                "Wall contact must approach through original room$4:$09 floor and retain its source layout.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,24,24);
            rom.Word(0xd00a,24*256+128); rom.Word(0xd00c,24*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count,int angle = 0xff) => StepSomariaMotionRom(rom,count,batch,angle,
                afterUpdate:() => {
                    var random = _random.CaptureState();
                    FailIf(_pushBlocks.Active || rom[0xd140] != 0 ||
                        _rooms.TilePushCounter != rom[0xcc6a] ||
                        random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls-seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        "Ordinary wall contact must retain native push clock, allocation, cues and RNG.");
                });
            Step(1);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(24,24);
                FailIf(!_player.IsPushing || rom[0xd034] != 0x10 || rom[0xcc65] != 3,
                    "Held wall contact must select native pushing WALK variant$04 and publish direction$03.");
                _dialogue.ShowGameplayMessage("Wall contact pause",100); rom[0xcba0] = 1;
                Step(3,24); _dialogue.Close(); rom[0xcba0] = 0;
                Step(2);
                FailIf(_player.IsPushing || rom[0xcc65] != 0xff,
                    "Releasing wall input must clear the push pose and late direction after text closes.");
            }
            Step(4,8);
            FailIf(_player.IsPushing || rom[0xd034] != 0,
                "Walking away from the original wall must restore ordinary WALK graphics.");
        }
    }
}
