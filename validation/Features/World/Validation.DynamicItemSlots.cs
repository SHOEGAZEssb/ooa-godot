using Godot;
using System;
using System.Reflection;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateDynamicItemSlots()
    {
        var pool=new DynamicItemSlotPool();
        object[] owners=[new(),new(),new(),new(),new()];
        bool[] live=[true,true,true,true,true];
        for(int i=0;i<5;i++)
        {
            int index=i;
            FailIf(pool.TryAllocate(owners[i],i==0||i==3?0x18:0x20,()=>live[index])!=0xd7+i,
                "getFreeItemSlot must allocate exactly the ascending $d7-$db pool.");
        }
        FailIf(pool.FindFree()!=-1 || pool.TryAllocate(new(),0x18,()=>true)!=-1 ||
            pool.FindItem(0x18)!=owners[0] || pool.FindItem(0x18,0xd7)!=owners[3],
            "Somaria must share a full five-slot pool and find old ITEM$18 in slot order.");
        // Marking an old block for replacement is not itemDelete. The full
        // allocator must continue to fail until its later handler retires it.
        FailIf(pool.FindFree()!=-1,"An old-block lookup must not release its slot.");
        live[3]=false; live[1]=false;
        object replacement=new();
        FailIf(pool.TryAllocate(replacement,0x18,()=>true)!=0xd8 || pool.FindFree()!=0xda || pool.SlotOf(owners[1])!=-1,
            "Dynamic item allocation must reuse the lowest deleted slot before deferred scene cleanup.");
        pool.Clear(); FailIf(pool.FindFree()!=0xd7,"Room clear must reset dynamic slot ownership.");
        bool firstLive=true;
        object first=new(), later=new(), earlier=new();
        pool.TryAllocate(first,0x18,()=>firstLive);
        var visited=new System.Collections.Generic.List<object>();
        foreach(object owner in pool.LiveOwners())
        {
            visited.Add(owner);
            if(owner!=first) continue;
            pool.TryAllocate(later,0x20,()=>true);
            firstLive=false;
            pool.TryAllocate(earlier,0x21,()=>true);
        }
        FailIf(!visited.SequenceEqual(new[]{first,later}) || !pool.LiveOwners().SequenceEqual(new[]{earlier,later}),
            "updateItems must visit a newly allocated later slot now and an already visited replacement on the next pass.");

        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        void Step(string? button=null) =>
            StepGameplayUpdates(1, Vector2.Zero, button is null?[]:[button], button is null?[]:[button], batched: true);
        var bombs=new BombEffect[5]; var record=new BombDatabase().Data;
        for(int i=0;i<5;i++) bombs[i]=_entities.Spawn<BombEffect>(new BombSpawn(_player,record,_rooms.ActiveGroup,_=>{}));
        FailIf(_entities.DynamicItemSlotAvailable || _entities.TrySpawnSwordBeam(_player.Position,0),
            "Live bombs must fill the shared item pool and block a sword-beam allocation.");
        _inventory.GiveTreasure(TreasureId.SwitchHook,1);
        _inventory.EquipA(TreasureId.SwitchHook);
        Step("attack"); Step();
        var hook=_entities.SwitchHook!.Item!;
        FailIf(hook.ChainAllocated || hook.ChainVisible,
            "Switch Hook's reserved weapon must remain usable while its dynamic chain allocation fails.");
        bombs[1].Discard();
        FailIf(!_entities.DynamicItemSlotAvailable,"itemDelete must release capacity before removal of the finished scene node.");
        Step();
        FailIf(!hook.ChainAllocated || !hook.ChainVisible || _entities.DynamicItemSlotAvailable,
            "Switch Hook must retry and occupy the newly released dynamic slot.");
        hook.Delete();
        FailIf(_entities.DynamicItemSlotAvailable,
            "Deleting the weapon before chain post must not release the chain's own item slot early.");
        hook.UpdatePost(_player.Position,false);
        FailIf(!_entities.DynamicItemSlotAvailable || hook.ChainAllocated,
            "Chain post must retire its slot after the related weapon disappears.");
        FailIf(!_entities.TrySpawnSwordBeam(_player.Position,0) || _entities.DynamicItemSlotAvailable,
            "The next sword beam must reuse the chain's released slot.");
        _entities.Clear();
        FailIf(!_entities.DynamicItemSlotAvailable,"Clearing live items must release shared allocation state.");
        var bomb=_entities.Spawn<BombEffect>(new BombSpawn(_player,record,_rooms.ActiveGroup,_=>{}));
        _entities.TrySpawnSwordBeam(_player.Position,0);
        var beam=(SwordBeamEffect)_entities.EntityAdapters<SwordBeamRoomEntity>().Single().Node;
        var textSource=_entities.TextActiveSource;
        try
        {
            _entities.TextActiveSource=()=>true;
            Step();
            FailIf(bomb.SetupPending || !beam.Initialized || bomb.ElapsedFrames!=1,
                "updateItems must initialize ITEM$03/$27 state0 even while text freezes initialized items.");
            Vector2 frozenBeam=beam.Position;
            Step();
            FailIf(bomb.ElapsedFrames!=1 || beam.Position!=frozenBeam,"Initialized ITEM$03/$27 must freeze during text.");
        }
        finally { _entities.TextActiveSource=textSource; }
        int observed=0;
        var observer=new ItemPhaseValidationEntity(()=>
        {
            observed++;
            FailIf(bomb.ElapsedFrames!=1+observed || !beam.Initialized,
                "The enemy pass must observe exactly one prior ITEM$03/$27 update, not a later interaction-phase update.");
        });
        typeof(RoomEntityManager).GetMethod("RegisterEnemySlot",flags)!.Invoke(_entities,[observer,0]);
        typeof(RoomEntityManager).GetMethod("AddEntity",flags)!.Invoke(_entities,[observer]);
        Step();
        input.CaptureForValidation([],[],Vector2.Zero);
        scheduler.Advance(2.0/60.0,update);
        FailIf(observed!=3 || bomb.ElapsedFrames!=4,"Dynamic items must advance once per gameplay update, including batched host frames.");
        _entities.Clear();
        GD.Print("Validated shared $d7-$db allocation and live slot traversal, bomb/beam item-before-enemy updates, state0 text eligibility, batched updates, and Switch Hook chain retry/post retirement.");
    }
}
