using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPushDestination()
    {
        int fixture = 0;
        foreach (bool clearBeforeDeadline in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            var (rom,synchronizer,seed) = PrepareSynchronizedBlockRom(0x9e,0x77,0);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void DestinationCollision(byte value)
            {
                // Declare another owner's changing collision byte at the
                // original destination. The contact/acceptance producer runs.
                _currentRoom.SetPositionTileAndCollision(new(120,72),0xa0,value,(long)_animationTicks);
                rom[0xcf47] = 0xa0; rom[0xce47] = value;
            }
            void Step(int count = 1,bool push = true) => StepSomariaMotionRom(rom,count,batch,push ? 0 : 0xff,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                CompareSynchronizedPushBlocksRom(rom,synchronizer,seed,sounds.Requests,
                    $"Destination deadline early={clearBeforeDeadline}, batch={batch}, update{++update}");
            });
            DestinationCollision(0x0f);
            for (int wait = 0; _pushBlocks.RemainingPushFrames != 19 && wait < 80; wait++)
            {
                Step();
                FailIf(_currentRoom.IsSolid(_player.Position),"Destination contact must approach through unchanged rows$67/$77 floor.");
            }
            FailIf(_pushBlocks.RemainingPushFrames != 19 || _pushBlocks.Active,
                "Original nextToPushableBlock must start contact before checking its blocked destination.");
            Step(18);
            FailIf(_pushBlocks.RemainingPushFrames != 1 || _pushBlocks.Active,
                "A blocked destination must retain all19 nonterminal contact updates.");
            if (clearBeforeDeadline) DestinationCollision(0);
            Step();
            if (!clearBeforeDeadline)
            {
                FailIf(_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 20 ||
                    _currentRoom.Layout[0x57] != 0x2a || sounds.Requests.Any(),
                    "Failed update20 must reset contact without allocating, changing the source or playing a cue.");
                DestinationCollision(0); Step(19);
                FailIf(_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 1,
                    "Clearing after rejection must require a fresh20 contact updates.");
                Step();
            }
            FailIf(!_pushBlocks.NativeInitialized || _currentRoom.Layout[0x57] != 0xa0 || rom[0xd146] != 31,
                "A clear destination on contact update20 must allocate, initialize and move in the same object pass.");
            Step(30,false);
            FailIf(!_pushBlocks.Active || rom[0xd146] != 1,"Accepted push must retain movement update31.");
            Step(1,false); Step(3,false);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Any() ||
                _currentRoom.Layout[0x47] != 0x2a || _currentRoom.Layout[0x27] != 0x2a ||
                sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != 2,
                "Both accepted statues must finish on update32 with one cue each and no delayed retry.");
        }
    }
}
