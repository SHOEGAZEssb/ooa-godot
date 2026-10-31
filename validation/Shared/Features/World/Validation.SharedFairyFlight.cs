using Godot;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedFairyFlight(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;var player=root._scene.Player;
        var entities=root._seasonsOpening?.Entities??root._entities;var memory=rooms.RuntimeState;
        var app=new ApplicationValidationFixture(root);var room=rooms.CurrentRoom;
        const int part=0xd0c0;
        foreach(var (position,angle) in new (Vector2,int)[]
            {(new(80.25f,4.5f),0),(new(156.25f,64.5f),8),(new(80.25f,124.5f),16),(new(4.25f,64.5f),24)})
        {
            entities.Clear();player.ClearNativeItemParents();player.Inventory.EquipA(0);player.Inventory.EquipB(0);
            var native=new BraceletRom(room,player.PrecisePosition,0,0xff,game:memory.Game);
            native.LoadSave(rooms.SaveData);native.InitializeWalkingAnimation();
            var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            var drop=entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0,position));
            native[part]=1;native[part+1]=1;
            native.Word(part+10,(int)(position.Y*256));native.Word(part+12,(int)(position.X*256));
            app.Step(1,Vector2.Zero,batched:batched,afterUpdate:()=>native.UpdateLinkItems(entities.FrameCounter,0,parts:true));
            // Declared post-bounce handoff, retaining real state-zero initialization and its RNG.
            void Stage(string name,object value)=>typeof(ItemDropEffect).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(drop,value);
            Stage("_state",DropState.Grounded);Stage("_counter",240);Stage("_fairyMovementCounter",10);
            Stage("_speed",0x28);Stage("_angle",angle);
            native[part+4]=2;native[part+6]=240;native[part+7]=10;native[part+9]=(byte)angle;native[part+0x10]=0x28;
            int calls=entities.Random.Calls,nativeCalls=native.RandomCalls;
            Step();
            FailIf(entities.Random.Calls!=calls+3 || native.RandomCalls!=nativeCalls+3 || drop.PrecisePosition!=position,
                $"{memory.Game}: fairy boundary angle${angle:x2} must reroll all three native flight values without movement.");
            memory.SetField("wDisabledObjects",8);native[0xcc8a]=8;Step(3);
            FailIf(entities.Random.Calls!=calls+3,"An initialized fairy must retain its RNG/counter while PART updates are disabled.");
            memory.SetField("wDisabledObjects",0);native[0xcc8a]=0;Step(4);
            entities.Clear();
            void Step(int count=1)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.UpdateLinkItems(entities.FrameCounter,0,parts:true);
                string context=$"{memory.Game}: fairy boundary angle${angle:x2}/batch{batched}";
                FailIf(native[part]==0 || drop.Finished || (int)drop.State!=native[part+4] || drop.FairyMovementCounter!=native[part+7] ||
                    drop.Angle!=native[part+9] || drop.Speed!=native[part+0x10] ||
                    drop.PrecisePosition!=new Vector2(native.Word(part+12)/256f,native.Word(part+10)/256f),
                    context+": native flight state/counter/angle/speed/position differs.");
                var rng=entities.Random.CaptureState();
                FailIf(rng.Calls-seed.Calls!=native.RandomCalls || rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95],context+": native RNG differs.");
            });
        }
    }
}
