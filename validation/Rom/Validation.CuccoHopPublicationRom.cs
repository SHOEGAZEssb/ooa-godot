using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCuccoHopPublicationRom()
    {
        foreach(bool giant in new[]{false,true})
        foreach(int angle in new[]{0,4,6,8,12,16,24,28})
        foreach(int hops in new[]{2,3,4,5})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(2,0xcf);_entities.Clear();
            FillValidationRoom(0);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,104));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var data=EnemyDatabase.Shared;var resources=new RoomEntityResources(_rooms);var actor=new CuccoCharacter();
            Vector2 initial=angle<8?new(8,8):new(80,64);
            actor.Initialize(data.ImportedEnemy(0x36),data.ImportedEnemy(0x3b),_currentRoom,initial,_random,
                resources.Bracelet.Data,resources.Bomb.Data,_sound.PlaySound,_=>{},(_,_,_,_)=>{});
            if(giant)typeof(CuccoCharacter).GetMethod("TransformIntoGiant",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(actor,null);
            var adapter=new CuccoRoomEntity(actor,data.GetRoomObjects(2,0xcf).Single());
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0;
            var after=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=(byte)(giant?0x9a:0x4c);
            const int at=0xd180;native[at]=1;native[at+1]=(byte)(giant?0x3b:0x36);
            native[at+11]=(byte)initial.Y;native[at+13]=(byte)initial.X;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldMoves=native.MovementCalls;
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;moves+=native.MovementCalls-oldMoves;
                FailIf(actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (int)actor.State!=native[at+4]||actor.Counter1!=native[at+6]||actor.Counter2!=native[at+7]||
                    actor.Angle!=native[at+9]||actor.Z!=unchecked((sbyte)native[at+15])||
                    actor.AnimationParameter!=native[at+0x21]||probes!=1||!observed.SequenceEqual(expected),
                    $"Cucco${(giant?0x3b:0x36):x2}/angle${angle:x2}/hops{hops}/fraction{fractional}/update{updates}: original hop differs: XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{actor.State}/{native[at+4]}, angle{actor.Angle}/{native[at+9]}, counters{actor.Counter1},{actor.Counter2}/{native[at+6]},{native[at+7]}, Z{actor.Z}/{unchecked((sbyte)native[at+15])}, anim{actor.AnimationParameter}/{native[at+0x21]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    $"Cucco${(giant?0x3b:0x36):x2}/update{updates}: common initialization or standing RNG differs: calls{rng.Calls-seed.Calls}/{native.RandomCalls}.");
                probes=0;
            });
            Step(1);
            F1Set(actor,"_state",CuccoState.Hopping);F1Set(actor,"_counter2",hops);F1Set(actor,"_angle",angle);
            native[at+4]=9;native[at+7]=(byte)hops;native[at+9]=(byte)angle;
            // Declared already-hopping boundary: original standing normally selects facing.
            int facing=(angle&15)==0?0:((angle&16)>>4)^1;
            actor.Animation.SetAnimation(facing);native[at+8]=(byte)facing;native.SetEnemyAnimation(1,facing);
            if(fractional){actor.Position+=new Vector2(.25f,.5f);native[at+10]=0x80;native[at+12]=0x40;}
            int motionStart=moves;Step(2);
            _dialogue.ShowMessage("Cucco hop hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(actor.State==CuccoState.Hopping&&updates<100)Step(1);
            int expectedMoves=hops switch{2=>16,3=>17,4=>32,_=>33};
            FailIf(actor.State!=CuccoState.Standing||moves-motionStart!=expectedMoves,
                "Cucco must still move on its final zero-Z update and retain the original16/17/32/33 hop boundaries.");
            var repeatSeed=_random.CaptureState() with {Rng1=0x40,Rng2=0};_random.RestoreState(repeatSeed);
            native[0xff94]=repeatSeed.Rng1;native[0xff95]=repeatSeed.Rng2;
            Step(1);FailIf(actor.State!=CuccoState.Hopping,"Cucco must start another source-selected hop after completion.");
            Step(16);FailIf(actor.State!=CuccoState.Standing,"Repeated source-selected two-hop cycle must finish after16 moving updates.");
            Step(2);_entities.Clear();_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
    }
}
