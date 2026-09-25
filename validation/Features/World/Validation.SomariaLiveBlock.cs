using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaLiveBlock()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach(bool batch in new[]{false,true})
        {
            LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(72,40));
            Vector2 point=new(72,70);
            // Isolated placement surface; no room progression or NPC approach.
            _currentRoom.SetPositionTileAndCollision(point,0x0c,0,0);
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            FailIf(!_entities.TryCreateSomariaBlock(_player,0,point,0),"ITEM$18 must allocate in the shared dynamic pool.");
            var block=_entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            Step(1); Step(8);
            FailIf(block.State!=1 || _currentRoom.GetMetatile(point)!=0x0c,
                "ITEM$18 must not write the solid tile before the ninth phase-animation update.");
            Step(1);
            FailIf(block.State!=3 || _currentRoom.GetMetatile(point)!=0xda || _currentRoom.GetTerrainInfo(point).Collision!=15,
                "Live ITEM$18 must publish tile$da/collision$f on phase update9.");
            var bombData=new BombDatabase().Data;
            for(int i=0;i<4;i++) _entities.Spawn<BombEffect>(new BombSpawn(_player,bombData,0,_=>{}));
            _entities.MarkPreviousSomariaBlock();
            FailIf((block.Flags&0x20)==0 || _entities.TryCreateSomariaBlock(_player,0,new(104,70),0),
                "Marking the old block must precede allocation and must not free a full item pool.");
            Step(1);
            FailIf(!block.Finished || _currentRoom.GetMetatile(point)!=0x0c || !_entities.DynamicItemSlotAvailable,
                "The old solid block must restore its tile and retire on its own next item update.");
            FailIf(!_entities.TryCreateSomariaBlock(_player,0,point,0),"A later swing must reuse the old block's slot.");
            Step(10);
            var next=_entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            FailIf(next.State!=3,"Repeated live block creation must finish phasing.");
            int puffs=_entities.EntityAdapters<IRoomEntity>().Count(e=>e.Node is PuzzlePuffEffect);
            _entities.ClearPhysicalPlayerItems();
            FailIf(next.Visible || (next.Flags&0x30)!=0x30 || next.Finished || _currentRoom.GetMetatile(point)!=0xda,
                "clearAllItems must hide/mark ITEM$18 without immediately deleting its tile or slot.");
            Step(1);
            FailIf(!next.Finished || _currentRoom.GetMetatile(point)!=0x0c || _entities.EntityAdapters<IRoomEntity>().Count(e=>e.Node is PuzzlePuffEffect)>puffs,
                "Marked ITEM$18 must restore its floor without a puff on the next eligible update.");
            _entities.Clear();
        }
        LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(72,40));
        Vector2 pickupPoint=new(72,70);
        _currentRoom.SetPositionTileAndCollision(pickupPoint,0x0c,0,0);
        void InputStep(int count,Vector2 movement,string[] held,string[] pressed)
        {
            input.CaptureForValidation(held,pressed,movement);
            scheduler.Advance(count/60.0,update);
        }
        for(int cycle=0;cycle<3;cycle++)
        {
        _entities.TryCreateSomariaBlock(_player,0,pickupPoint,0);
        InputStep(11,Vector2.Zero,[],[]);
        var carried=_entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
        _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet,1);
        _inventory.EquipA(InventoryState.ItemBracelet);
        InputStep(24,Vector2.Down,["move_down"],["move_down"]);
        FailIf(_player.Position.Y>=64 || _player.Position.Y<54,
            $"Somaria pickup approach must stop outside the solid tile, got Link {_player.Position}.");
        InputStep(1,Vector2.Zero,["attack"],["attack"]);
        FailIf(!carried.IsHeld || !_player.IsCarryingObject || _currentRoom.GetMetatile(pickupPoint)!=0x0c,
            "Bracelet input must pick up a reachable Somaria block and restore its floor in the same item pass.");
        if(cycle==2)
        {
            int beforePuffs=_entities.EntityAdapters<IRoomEntity>().Count(e=>e.Node is PuzzlePuffEffect);
            _entities.ClearPhysicalPlayerItems();
            FailIf(carried.IsHeld || carried.Substate!=3 || _player.IsCarryingObject ||
                _player.BraceletLiftCollisionsDisabled || carried.Visible || carried.Flags!=0x30,
                "Physical item clearing during lift must clear the parent/holder, drop ITEM$18 to substate3, and hide/mark it immediately.");
            InputStep(1,Vector2.Zero,[],[]);
            FailIf(!carried.Finished || _currentRoom.GetMetatile(pickupPoint)!=0x0c ||
                _entities.EntityAdapters<IRoomEntity>().Count(e=>e.Node is PuzzlePuffEffect)>beforePuffs,
                "The dropped and marked Somaria block must retire quietly without leaking the lift pose or source tile.");
            continue;
        }
        InputStep(30,Vector2.Zero,["attack"],[]);
        InputStep(1,Vector2.Zero,[],[]);
        InputStep(1,Vector2.Zero,["attack"],["attack"]);
        FailIf(carried.IsHeld || _player.IsCarryingObject,
            "A second Bracelet press must release the live block through its ITEM$18 throw state.");
        InputStep(30,Vector2.Zero,[],[]);
        FailIf(!carried.Finished,"A stationary released Somaria block must retire on landing.");
        }
        _entities.Clear();
        GD.Print("Validated live Somaria block item dispatch, phase boundary, shared-pool replacement failure/retry, floor restoration and quiet item clearing in single/batched updates.");
    }
}
