using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownPushContact()
    {
        var cases = new[] { 0f,0.25f,0.75f }.Select(fraction => (Room:0x9e,Fraction:fraction,Direction:0))
            .Concat(from direction in new[] { 0,1,2,3 }
                from fraction in new[] { 0f,0.25f,0.75f }
                select (Room:0xbc,Fraction:fraction,Direction:direction)).ToArray();
        int fixture = 0;
        foreach (var item in cases)
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            Vector2 direction = OracleObjectMath.StrictCardinalVector(item.Direction*8);
            Vector2 start = (item.Room == 0x9e ? new Vector2(120,120) : new Vector2(104,72)-direction*16)+
                Vector2.One*item.Fraction;
            SomariaRom rom; OracleRandomState seed; PushBlockSynchronizerRoomEntity? synchronizer = null;
            if (item.Room == 0x9e)
            {
                (rom,synchronizer,seed) = PrepareSynchronizedBlockRom(0x9e,0x77,0);
                FailIf(_currentRoom.Layout[0x37] != 0x2a || _currentRoom.Layout[0x57] != 0x2a,
                    "Original room049e.bin must retain both source statues for repeated contact.");
            }
            else
            {
                ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xbc); _entities.Clear();
                _inventory.GiveTreasure(TreasureId.Bracelet,1); _inventory.EquipA(0); _inventory.EquipB(0);
                seed = _random.CaptureState();
                rom = new SomariaRom(_saveData,seed,_currentRoom,item.Direction,(int)start.X,(int)start.Y);
                rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
                FailIf(_currentRoom.Layout[0x46] != 0x2a,"Original room04bc.bin must retain statue$46:$2a.");
            }
            _player.WarpTo(start); _player.Face((Vector2I)direction);
            rom.Word(0xd00c,(int)(start.X*256)); rom.Word(0xd00a,(int)(start.Y*256));
            FailIf(_collision.Collides(start),"Fractional statue contact must start on unchanged source floor.");
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool push = true) => StepSomariaMotionRom(rom,count,batch,push ? item.Direction*8 : 0xff,
                afterUpdate:() => {
                    rom.AdvanceTileGraphics();
                    string context = $"Statue contact4:{item.Room:x2}, fraction={item.Fraction}, angle${item.Direction*8:x2}, batch={batch}, update{++update}";
                    if (synchronizer != null)
                        CompareSynchronizedPushBlocksRom(rom,synchronizer,seed,sounds.Requests,context);
                    else
                    {
                        CompareNativePushBlockRom(rom,_pushBlocks,0xd140,context);
                        var random = _random.CaptureState();
                        FailIf(_rooms.BlockPushAngle != rom[0xcca6] || _pushBlocks.RemainingPushFrames != rom[0xcc6a] ||
                            random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                            context+": shared direction/contact counter/cues/RNG differ.");
                    }
                });
            Step(1,false);
            for (int repeat = 0; repeat < (item.Room == 0x9e ? 2 : 1); repeat++)
            {
                for (int wait = 0; !_pushBlocks.Active && wait < 80; wait++)
                {
                    Step();
                    FailIf(_currentRoom.IsSolid(_player.Position),"Actual statue approach must keep Link's center on source floor.");
                }
                FailIf(!_pushBlocks.NativeInitialized || rom[0xd146] != 31,
                    "Actual statue contact must allocate, initialize and move once before the clamp checks.");
                if (item.Room == 0x9e)
                {
                    // Original$14 center is tile Y+6; objectApplySpeed moves
                    // $0080 before objectPreventLinkFromPassing writes only yh.
                    float expectedY = 98-repeat*16+item.Fraction;
                    FailIf(_player.PrecisePosition != new Vector2(120+item.Fraction,expectedY),
                        "Initial native clamp must retain both Link fractions and the source Y-2 object offset.");
                    Step(2);
                    FailIf(_player.PrecisePosition != new Vector2(120+item.Fraction,expectedY-2),
                        "Link must move before the block's next two clamp updates.");
                    Step(2);
                    FailIf(_player.PrecisePosition != new Vector2(120+item.Fraction,expectedY-3),
                        "Following the half-pixel block must advance Link one pixel every two updates.");
                    Step(27);
                    FailIf(_pushBlocks.Active || _player.PrecisePosition !=
                        new Vector2(120+item.Fraction,82-repeat*16+item.Fraction),
                        "Final update32 must clamp Link before destination tile placement and deletion.");
                }
                else
                {
                    int previous = 1;
                    foreach (int movementUpdate in new[] { 16,31,32 })
                    {
                        Step(movementUpdate-previous);
                        Vector2 block = (new Vector2(104,70)+direction*(movementUpdate*0.5f)).Floor();
                        Vector2 expected = new Vector2(104,72)+Vector2.One*item.Fraction;
                        if (direction.X != 0) expected.X = block.X-direction.X*12+item.Fraction;
                        else expected.Y = block.Y-direction.Y*12+item.Fraction;
                        FailIf(_player.PrecisePosition != expected || _pushBlocks.Active != (movementUpdate < 32),
                            $"Native trailing-edge clamp angle${item.Direction*8:x2}/update{movementUpdate} must retain fractions and the32-update lifetime.");
                        previous = movementUpdate;
                    }
                }
            }
            Step(3,false);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Any() ||
                sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != (item.Room == 0x9e ? 4 : 1),
                "Finished contact must leave no moving block or delayed cue after repeated use.");
        }
    }
}
