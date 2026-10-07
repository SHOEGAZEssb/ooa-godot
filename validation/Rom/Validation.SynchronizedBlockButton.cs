using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockButton()
    {
        int fixture = 0;
        foreach (bool primaryButton in new[] { true,false })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            var (rom,synchronizer,seed) = PrepareSynchronizedBlockRom(0x9e,0x59,3);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) => StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                string context = $"Synchronized button primary={primaryButton}, batch={batch}, update={++update}";
                CompareSynchronizedPushBlocksRom(rom,synchronizer,seed,sounds.Requests,context);
                FailIf(_rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                    context+": original tile-write/graphics-drain queue count differs.");
            });
            void BeginPush(int angle)
            {
                for (int wait = 0; !_pushBlocks.Active && wait < 80; wait++)
                {
                    Step(angle:angle);
                    FailIf(_currentRoom.IsSolid(_player.Position),
                        $"Synchronized statue approach must retain a floor center: angle${angle:x2}, update{update}, Link={_player.PrecisePosition}.");
                }
                FailIf(!_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 1,
                    "Actual statue contact must start both original synchronized actors.");
            }
            Step();
            // Reach$35/$55 by two real leftward pushes of source$37/$57.
            // No completed movement or source layout is injected.
            for (int push = 0; push < 2; push++)
            {
                BeginPush(24); Step(30);
                FailIf(!_pushBlocks.Active,"Leftward staging must retain native movement update31.");
                Step();
                FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Any(),
                    "Both real leftward pushes must complete on movement update32.");
            }
            FailIf(_currentRoom.Layout[0x35] != 0x2a || _currentRoom.Layout[0x55] != 0x2a ||
                _currentRoom.Layout[0x37] != 0xa0 || _currentRoom.Layout[0x57] != 0xa0,
                "Executed synchronized pushes must reach original column$05 before the height comparison.");
            Step(16,16);
            for (int wait = 0; _player.PrecisePosition.X > 88.25f && wait < 48; wait++) Step(angle:24);
            FailIf(_player.PrecisePosition != new Vector2(88.25f,104.5f) || _collision.Collides(_player.Position),
                "Link must walk below the staged statues through unchanged row$06 floor.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // Declare an unpressed button tile under either destination.
                // PART pressure/trigger publication is outside this shared$14
                // height-rule comparison and has separate ROM scenarios.
                byte button = (byte)((primaryButton ? 0x45 : 0x25)-repeat*0x10);
                FailIf(!_rooms.TrySetTile(button,0x0c),"Declared button tile must fit the canonical queue.");
                rom.SetTile(button,0x0c);
                BeginPush(0);
                var partner = _entities.Entities<PushBlockController>().Single();
                FailIf(_pushBlocks.BlockZHigh != 0 || partner.BlockZHigh != 0,
                    "Both source statues must begin at zero height before the button row.");
                Step(12);
                FailIf(_pushBlocks.BlockTopLeft != new Vector2(80,73.5f-repeat*16) || _pushBlocks.BlockZHigh != 0,
                    "Update13 crosses the button row after the native pre-movement height sample.");
                Step();
                FailIf(_pushBlocks.BlockZHigh != (primaryButton ? -2 : 0) || partner.BlockZHigh != (primaryButton ? 0 : -2) ||
                    _pushBlocks.BlockTopLeft != new Vector2(80,73-repeat*16),
                    "Update14 must raise only the statue above the unpressed button while preserving world XY.");
                Step(17);
                FailIf(!_pushBlocks.Active || _pushBlocks.BlockZHigh != (primaryButton ? -2 : 0) ||
                    partner.BlockZHigh != (primaryButton ? 0 : -2),
                    "Native button height must persist through movement update31.");
                Step(); Step(3);
                FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Any() ||
                    _currentRoom.Layout[0x45-repeat*0x10] != 0x2a || _currentRoom.Layout[0x25-repeat*0x10] != 0x2a,
                    "Update32 must place both statues at their XY destinations and leave no delayed actor after completion.");
            }
        }
    }
}
