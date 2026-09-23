using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaPush()
    {
        var tiles=new PushableTileDatabase();
        for(int mode=0;mode<6;mode++)
            FailIf(!tiles.TryGetSomaria(mode,0xda,out byte parameter) || parameter!=0x80 || tiles.TryGet(mode,0xda,out _),
                $"Collision mode${mode:x2} must dispatch tile$da to ITEM$18 with parameter$80, not INTERAC_PUSHBLOCK.");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach(bool batch in new[]{false,true})
        foreach(int level in new[]{1,2})
        {
            LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(72,40));
            for(int y=70;y<=102;y+=16) _currentRoom.SetPositionTileAndCollision(new(72,y),0x0c,0,0);
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,level);
            typeof(InventoryState).GetMethod("SetVariable",flags)!.Invoke(_inventory,[TreasureVariable.BraceletLevel,level]);
            void Step(int count,bool down=false)
            {
                input.CaptureForValidation(down?["move_down"]:[],[],down?Vector2.Down:Vector2.Zero);
                if(batch) scheduler.Advance(count/60.0,update);
                else for(int i=0;i<count;i++) scheduler.Advance(1.0/60.0,update);
            }
            _entities.TryCreateSomariaBlock(_player,0,new(72,70),0);
            Step(11);
            var block=_entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            for(int cycle=0;cycle<2;cycle++)
            {
                for(int i=0;i<50 && _pushBlocks.RemainingPushFrames==20;i++) Step(1,true);
                FailIf(_pushBlocks.RemainingPushFrames!=19 || block.State!=3,
                    $"Somaria must be reachable by pushing from floor, cycle{cycle}, Link {_player.Position}.");
                // Releasing input cancels the countdown, then a new sustained
                // push must consume the full source20 updates.
                Step(1);
                FailIf(_pushBlocks.RemainingPushFrames!=20,"Releasing a Somaria push must reset its counter.");
                Step(19,true);
                FailIf(_pushBlocks.RemainingPushFrames!=1 || block.State!=3 || _pushBlocks.Active,
                    "Somaria must remain still through push update19 without allocating the ordinary push interaction.");
                Step(1,true);
                Vector2 start=new(72,70+cycle*16);
                FailIf(block.State!=4 || block.Substate!=0 || block.Position!=start || _pushBlocks.Active,
                    "Push update20 must signal ITEM$18 and enter state4 without movement until its following handler update.");
                int moves=level==2?21:32;
                Step(1);
                FailIf(block.Counter!=moves-1 || _currentRoom.GetMetatile(start)!=0x0c,
                    $"First Somaria movement update must restore the source floor and consume one speed-dependent counter: level{level}/{_inventory.BraceletLevel}, cycle{cycle}, state{block.State}:{block.Substate}, counter{block.Counter}, tile${_currentRoom.GetMetatile(start):x2}.");
                Step(moves-2);
                FailIf(block.Counter!=1 || block.State!=4,"Somaria must remain moving until the final source counter update.");
                Step(1);
                FailIf(block.State!=3 || block.Position!=start+Vector2.Down*16 ||
                    _currentRoom.GetMetatile(start+Vector2.Down*16)!=0xda,
                    "Somaria movement must finish aligned on the next tile after32 normal or21 Power Glove updates.");
            }
            // A live collision override must win over the floor tile's
            // ordinary collision-table entry, and be checked only at zero.
            _currentRoom.SetPositionTileAndCollision(new(72,118),0x0c,15,0);
            for(int i=0;i<50 && _pushBlocks.RemainingPushFrames==20;i++) Step(1,true);
            Step(18,true);
            FailIf(_pushBlocks.RemainingPushFrames!=1 || block.State!=3,
                "A blocked Somaria destination must still consume the source push countdown.");
            Step(1,true);
            FailIf(_pushBlocks.RemainingPushFrames!=20 || block.State!=3 || _pushBlocks.Active,
                "Raw destination collision$f must reject the zero-counter push and reset without moving the block.");
            _currentRoom.SetPositionTileAndCollision(new(72,118),0x0c,0,0);
            Step(20,true);
            FailIf(block.State!=4,"Clearing the destination must permit the next complete push attempt.");
            _entities.ClearPhysicalPlayerItems();
            Step(1);
            FailIf(!block.Finished || _currentRoom.GetMetatile(new(72,102))!=0x0c,
                "Cancelling a newly signalled Somaria push must restore its source tile before retiring.");
            _entities.Clear();
        }
        GD.Print("Validated Somaria's six source push dispatch rows and reachable repeated pushes, cancellation, exact20 delay and32/21 movement boundaries in single/batched gameplay updates.");
    }
}
