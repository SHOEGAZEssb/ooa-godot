using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGiantGhiniChildRom()
    {
        foreach(int index in Enumerable.Range(0,3))
        foreach(bool intro in new[]{false,true})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            InitializeCompletedIntroRoom(4, 0x18);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,88));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(AgesWramAddress.wScrollMode,0);
            var data=new DungeonBossDatabase();var parent=new GiantGhiniBoss();
            parent.Initialize(data.Enemy(0x70),_currentRoom,new(120.25f,88.5f),_random,_sound.PlaySound,
                ()=>false,_entities.DisableLinkCollisionsAndMenu,()=>{},_entities.ToScreen);
            F1Set(parent,"_initialized",true);F1Set(parent,"_state",intro?GiantGhiniBossBossState.IntroWait:GiantGhiniBossBossState.Moving);
            F1Set(parent,"_childrenAlive",1);
            var child=new GiantGhiniChild();child.Initialize(data.Enemy(0x3f),parent,_currentRoom,index,_random);
            // Fresh native allocation copies only the parent's high XY bytes.
            // A separate declared retained-low variant checks every high-only write.
            if(fractional)child.Position=new(120.25f,88.5f);
            var adapter=new GiantGhiniChildRoomEntity(child,_sound.PlaySound);
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,0);_entities.AddEntity(before);
            _entities.RegisterEnemySlot(adapter,1);_entities.AddEntity(adapter);
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0;
            var after=new PartMovementScratchProbe(observedCapture.Capture);
            _entities.RegisterEnemySlot(after,15);_entities.AddEntity(after);
            var seed=_random.CaptureState();var native=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;native[0xcc08]=0xad;
            // Declared healthy, stationary parent fields isolate the complete child
            // handler from body AI. Actual body/contact phase coverage is separate.
            native[0xd084]=(byte)(intro?8:9);native[0xd0a9]=17;native[0xd0b0]=1;
            const int at=0xd180;native[at]=1;native[at+1]=0x3f;native[at+2]=(byte)((intro?0x80:0)|3-index);
            native[at+0x17]=0xd0;native.Word(at+10,fractional?0x5880:0x5800);native.Word(at+12,fractional?0x7840:0x7800);
            int SourceState()=>child.InitializationPending?0:child.State switch
            {ChildState.Waiting=>8,ChildState.SpawnDelay=>9,ChildState.Charging=>10,ChildState.Attached=>11,_=>12};
            void Step(int count,int pressed=0)=>StepGameplayUpdates(count,Vector2.Zero,pressed:MenuRomActions(pressed),batched:batched,afterUpdate:()=>
            {
                byte[] expected=new byte[4];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native[0xccd8]=0;native[0xcc61]&=15;
                native[0xcc2a]=(byte)pressed;
                native.Update(_entities.FrameCounter,_player.Position,afterEnemies:()=>
                {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                FailIf(child.IsDead!=(native[at]==0)||!child.IsDead&&(SourceState()!=native[at+4]||child.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SomariaPrivate<int>(child,"_counter")!=native[at+6]||SomariaPrivate<int>(child,"_angle")!=native[at+9]||
                    child.Visible!=((native[at+0x1a]&128)!=0)||child.AnimationParameter!=native[at+0x21]||
                    SomariaPrivate<int>(child.Animation,"_frameCounter")!=native[at+0x20]||child.ZHigh!=unchecked((sbyte)native[at+15]))||
                    _entities.PlayerSwordDisabled!=((native[0xccd8]&32)!=0)||_entities.PlayerMovementDisabled!=((native[0xcc61]&32)!=0)||
                    SomariaPrivate<bool>(parent,"_childRequestsCharge")!=(native[0xd0b2]!=0)||observedCapture.Samples!=1||!observedCapture.Bytes.SequenceEqual(expected),
                    $"Giant Ghini child/index{index}/intro{intro}/fraction{fractional}/update{updates}: state{SourceState()}/{native[at+4]}, XY{child.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, C1{SomariaPrivate<int>(child,"_counter")}/{native[at+6]}, angle{SomariaPrivate<int>(child,"_angle")}/{native[at+9]}, clock{SomariaPrivate<int>(child.Animation,"_frameCounter")}/{native[at+0x20]}, restrictions{_entities.PlayerSwordDisabled}/{_entities.PlayerMovementDisabled}, scratch[{string.Join(',',observedCapture.Bytes)}]/[{string.Join(',',expected)}].");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    $"Giant Ghini child/update{updates}: common RNG calls{_random.CaptureState().Calls-seed.Calls}/{native.RandomCalls}.");observedCapture.Samples=0;
            });
            try
            {
                _dialogue.ShowMessage("Giant child state0 hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                Step(1);Step(2);
                if(intro){F1Set(parent,"_state",GiantGhiniBossBossState.Moving);native[0xd084]=9;Step(1);}
                else Step(28);
                FailIf(child.State!=ChildState.Charging,"The child must complete waiting/delay before charging.");
                Step(19);
                // Exact common-nudge half turn, independently declared at its
                // caller boundary; movement still runs before target selection.
                _player.WarpTo(new(120,88));child.Position=new(120,80);native.Word(at+10,80*256);native.Word(at+12,120*256);
                F1Set(child,"_angle",0);F1Set(child,"_counter",1);native[at+9]=0;native[at+6]=1;Step(1);
                FailIf(SomariaPrivate<int>(child,"_angle")!=1,"The original half turn must increment the child angle.");
                _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                Step(8);
                // Declare an admitted post-object Link hit. This checks the
                // next-handler boundary; it does not replace real collision coverage.
                _player.WarpTo(child.Position.Floor());child.HandleLinkContact(_player);native[at+0x2a]=0x80;
                FailIf(child.State!=ChildState.Charging||SomariaPrivate<bool>(parent,"_childRequestsCharge"),
                    "Contact must queue JUST_HIT without synchronously attaching or signalling the parent.");
                Step(1);FailIf(child.State!=ChildState.Attached||SomariaPrivate<int>(child,"_counter")!=119,
                    "The first attached handler must decrement120 immediately.");
                _dialogue.ShowMessage("Attached child hold.",104);native[0xcba0]=1;Step(2);_dialogue.Close();native[0xcba0]=0;
                Step(1);F1Set(child,"_counter",4);native[at+6]=4;Step(1,1);
                FailIf(SomariaPrivate<int>(child,"_counter")!=0,"DEC4 then a fresh key must subtract3 to zero without detaching.");
                Step(1);FailIf(SomariaPrivate<int>(child,"_counter")!=255,"The next attached counter visit must wrap zero to ff.");
                F1Set(child,"_counter",1);native[at+6]=1;Step(1);
                FailIf(child.State!=ChildState.Fading||SomariaPrivate<bool>(parent,"_childRequestsCharge"),"Detachment must clear the parent signal on its own child handler.");
                Step(59);Step(1);
                FailIf(!child.IsDead||native[at]!=0||parent.ChildrenAlive!=0,"The sixtieth fading visit must delete and decrement the parent exactly once.");
                _entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
            }
            finally{parent.Free();}
        }
    }
}
