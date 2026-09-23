using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSynchronizedPushBlocks()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        Vector2 Center(int position) => new((position & 15)*16+8,(position >> 4)*16+8);
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1,Vector2 movement = default)
            {
                input.CaptureForValidation([],[],movement);
                if (batch) scheduler.Advance(count/60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0/60.0,update);
            }
            void ApproachAndPush()
            {
                for (int i = 0; !_pushBlocks.Active && i < 80; i++)
                {
                    Step(movement:Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Statue approach entered solid room geometry.");
                }
                FailIf(!_pushBlocks.Active,"Real northward approach failed to push Crown statue.");
            }
            LoadValidationRoom(4,0x9e);
            FailIf(_rooms.BlockPushAngle != 0,"Room reload must clear wBlockPushAngle ($cca6).");
            _player.WarpTo(Center(0x77));
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1);
            var synchronizer = _entities.Entities<PushBlockSynchronizerRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(synchronizer) != 3 || _currentRoom.IsSolid(_player.Position),
                "4:9e must place $bd at slot $d3 and permit the initial floor approach.");
            // Independent room049e.bin transcription: statues $37/$57,
            // clear rows $17/$27/$47/$67/$77, wall above at $07.
            FailIf(_currentRoom.GetMetatile(Center(0x37)) != 0x2a || _currentRoom.GetMetatile(Center(0x57)) != 0x2a,
                "4:9e source statue positions changed.");
            for (int repeat = 0; repeat < 3; repeat++)
            {
                ApproachAndPush();
                FailIf(_rooms.BlockPushAngle != 0x80,"Upward INTERAC$14 initialization must publish $80 to wBlockPushAngle.");
                var partners = _entities.Entities<PushBlockController>();
                if (repeat < 2)
                {
                    FailIf(partners.Count != 1 || _entities.InteractionSlot(partners[0]) != 4,
                        "Synchronizer must allocate the other statue at $d4 in the initiating update.");
                    var moving = partners[0];
                    FailIf(moving.BlockTopLeft.Y != (3-repeat)*16-0.5f ||
                        _pushBlocks.BlockTopLeft.Y != (5-repeat)*16-0.5f,
                        "Reserved and ordinary $14 must each move half a pixel on their first dispatch.");
                    if (repeat == 1)
                    {
                        var text = _entities.TextActiveSource;
                        Vector2 primaryBefore = _pushBlocks.BlockTopLeft, partnerBefore = moving.BlockTopLeft;
                        try
                        {
                            _entities.TextActiveSource = () => true;
                            Step(8);
                            FailIf(_pushBlocks.BlockTopLeft != primaryBefore || moving.BlockTopLeft != partnerBefore,
                                "Text must freeze initialized reserved and ordinary push interactions equally.");
                        }
                        finally { _entities.TextActiveSource = text; }
                    }
                }
                else FailIf(partners.Count != 0,"The statue at $17 must not move into the original wall at $07.");
                Step(30);
                FailIf(!_pushBlocks.Active,"Native $14 must still be active after movement update31.");
                Step();
                FailIf(_rooms.BlockPushAngle != 0x80,"Deleting completed blocks must retain the last shared push direction.");
                FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 0 ||
                    _currentRoom.GetMetatile(Center(0x47-repeat*0x10)) != 0x2a ||
                    _currentRoom.GetMetatile(Center(repeat == 0 ? 0x27 : 0x17)) != 0x2a,
                    "Native statue movement must finish on update32 and preserve a blocked partner.");
            }
            LoadValidationRoom(4,0x9e); _player.WarpTo(Center(0x77));
            FailIf(_rooms.BlockPushAngle != 0,"Re-entering the same room must clear the prior push direction.");
            // A non-solid-to-Link special collision still blocks the $bd raw
            // byte probe. Clearing it while the primary moves permits a retry.
            _currentRoom.SetPositionTileAndCollision(Center(0x27),0xa0,0x10,(long)_animationTicks);
            ApproachAndPush();
            FailIf(_entities.Entities<PushBlockController>().Count != 0,
                "INTERAC $bd must reject raw destination collision $10.");
            _currentRoom.SetPositionTileAndCollision(Center(0x27),0xa0,0,(long)_animationTicks);
            Step();
            FailIf(_entities.Entities<PushBlockController>().Count != 0,
                "INTERAC $bd state2 must spend one update returning to state1.");
            Step();
            FailIf(_entities.Entities<PushBlockController>().Count != 1,
                "INTERAC $bd must retry a newly clear destination while the reserved push is active.");
            LoadValidationRoom(0,0x60); Step(4);
            LoadValidationRoom(4,0x9e);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 0 ||
                _currentRoom.GetMetatile(Center(0x37)) != 0x2a || _currentRoom.GetMetatile(Center(0x57)) != 0x2a,
                "Room departure must cancel both moving blocks; re-entry restores the source statues.");

            LoadValidationRoom(4,0x9b); _player.WarpTo(Center(0x25));
            // room049b.bin has blue blocks $35/$43/$56. Pushing $35 down
            // finds $56 before $43; red/yellow blocks must remain unchanged.
            for (int i = 0; !_pushBlocks.Active && i < 80; i++)
            {
                Step(movement:Vector2.Down);
                FailIf(_currentRoom.IsSolid(_player.Position),"Blue-block approach entered solid geometry.");
            }
            FailIf(!_pushBlocks.Active,"Actual room 4:9b approach failed to push blue block $35.");
            var blue = _entities.Entities<PushBlockController>().OrderBy(block => _entities.InteractionSlot(block)).ToArray();
            FailIf(blue.Length != 2 || blue[0].BlockTopLeft != new Vector2(96,80.5f) ||
                blue[1].BlockTopLeft != new Vector2(48,64.5f),
                "INTERAC $bd must allocate matching blue blocks in descending packed-position order.");
            Step(31);
            foreach (int position in new[] { 0x45,0x53,0x66 })
                FailIf(_currentRoom.GetMetatile(Center(position)) != 0x2e,
                    $"Blue block did not reach source-derived destination ${position:x2}.");
            foreach (int position in new[] { 0x55,0x79 })
                FailIf(_currentRoom.GetMetatile(Center(position)) != 0x2c,"Blue synchronization moved a red block.");
            foreach (int position in new[] { 0x4a,0x5c,0x6a })
                FailIf(_currentRoom.GetMetatile(Center(position)) != 0x2d,"Blue synchronization moved a yellow block.");
            FailIf(_rooms.BlockPushAngle != 0x90,"The completed downward push must retain shared angle $90.");

            // Isolate the dynamic INTERAC$14 initialization writer. Its
            // direction differs from the reserved block's last movement.
            var independent = _pushBlocks.CreateSynchronizedController();
            try
            {
                independent.StartNativeMovement(0x53,0x08,1);
                FailIf(!independent.Active || _rooms.BlockPushAngle != 0x88 || _pushBlocks.PushAngle != 0x08,
                    "A dynamic block must overwrite the shared angle read by $bd/$dc, independent of the reserved block.");
                independent.Cancel();
                _pushBlocks.Cancel();
                FailIf(_rooms.BlockPushAngle != 0x88,"Cancelling push objects must not clear wBlockPushAngle.");
                var preload = _rooms.GetRoom(4,0x9e);
                FailIf(_rooms.BlockPushAngle != 0x88,"Reading destination room data must preserve the live push signal.");
                _rooms.SetLoadedRoom(4,preload);
                FailIf(_rooms.BlockPushAngle != 0,"Scroll-entry room activation must clear wBlockPushAngle.");
                independent.StartNativeMovement(0x37,0x18,1);
                FailIf(_rooms.BlockPushAngle != 0x98,"Dynamic leftward push must publish $98.");
                _rooms.LoadCutsceneRoom(4,0x9b);
                FailIf(_rooms.BlockPushAngle != 0,"Cutscene room loading must clear wBlockPushAngle.");
            }
            finally { independent.Free(); }
            LoadValidationRoom(4,0x9b);
        }
    }
}
