using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRalphRaftonMovementRom()
    {
        foreach(int linkX in new[]{48,80})
        foreach(bool late in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetGlobalFlag(0x15);LoadValidationRoom(1,0x97);
            var owner=_roomEvents.Get<RalphAfterRaftonEvent>();var actor=_entities.Entities<RalphAfterRaftonCharacter>().Single();
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity=>ReferenceEquals(entity.Node,actor)));
            FailIf(actor.Record is not{X:0x38,Y:0x38},"Ralph$37:$03's source placement changed.");
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(linkX+0.75f,104.25f));
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            F1Set(owner,"_precisePosition",new Vector2(56.25f,56.5f));actor.Position=new(56,56);
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,linkX,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x37;native[at+2]=3;native[at+11]=56;native[at+13]=56;native.AdvanceInteractions();
            native.Word(at+10,0x3880);native.Word(at+12,0x3840);native.Word(0xd00a,0x6840);native.Word(0xd00c,linkX*256+0xc0);native[0xcd00]=0;
            int updates=0,moves=0,hold=0,lateCount=0;bool observing=false,injectThisUpdate=false;
            bool lateWait=false,lateApproach=false,lateScript=false;
            int writer=at-256;
            byte[] observed=new byte[4],expected=new byte[4];
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                if(!late||native[at]==0||native[0xcba0]!=0)return;
                bool inject=false;
                if(!lateWait&&native[at+5]==4&&native[at+6]==0x10){lateWait=true;inject=true;}
                if(!lateApproach&&native[at+5]==5&&native[at+6]==0x2c){lateApproach=true;inject=true;}
                if(!lateScript&&native[at+5]==8&&native[at+7]==0x40){lateScript=true;inject=true;}
                if(!inject)return;
                FailIf(writer<0xd040,"Ralph late-text fixture needs an earlier native slot.");
                native[writer]=1;native[writer+1]=0x3c;native[writer+2]=1;native[writer+4]=1;
                native.Word(writer+0x18,0xc180);native[0xc180]=0x98;native[0xc181]=0x57;native[0xc182]=3;
                injectThisUpdate=true;lateCount++;
                _dialogue.ShowMessage("Earlier interaction opens text.",_player.Position.Y);
            }));
            void Observe()
            {
                if(!observing||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                observing=false;
            }
            void Seed(){observing=false;for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}observing=true;}
            _runtimeState.Changed+=Observe;
            try
            {
                Seed();
                while(native[at]!=0&&updates<900)
                {
                    StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:()=>
                    {
                        if(native[at]==0)
                        {
                            FailIf(owner.HasState||actor.Active||!observing,"Retired Ralph must stop moving on subsequent updates.");
                            Seed();return;
                        }
                        int x=native.Word(at+12),y=native.Word(at+10);
                        native.UpdateGameplay(0,0,0xff,_entities.FrameCounter,afterInteractions:()=>
                        {for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                        bool moved=native[at]!=0&&(x!=native.Word(at+12)||y!=native.Word(at+10));if(moved)moves++;
                        int counter1=owner.Substate<8?owner.Counter:runner.NativeCounter1;
                        FailIf(actor.Active!=(native[at]!=0)||owner.HasState!=(native[at]!=0)||actor.Active&&(
                            owner.Substate!=native[at+5]||counter1!=native[at+6]||runner.MovementCounter("Ralph")!=native[at+7]||
                            SomariaPrivate<Vector2>(owner,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            owner.ZFixed!=unchecked((short)native.Word(at+14))||owner.SpeedZ!=unchecked((short)native.Word(at+0x14))||
                            actor.CurrentAnimationCounter!=native[at+0x20]||actor.CurrentAnimationParameter!=native[at+0x21])||
                            moved&&(observing||!observed.SequenceEqual(expected))||!moved&&!observing||
                            _dialogue.IsOpen!=(native[0xcba0]!=0),
                            $"Ralph after Rafton update{updates}/late{late}: original native motion/counter/animation/text/publication differs (active={actor.Active}/{native[at]}, command={owner.CurrentCommandIndex}, state={owner.Substate}/{native[at+5]}, counter={counter1}/{native[at+6]}, counter2={runner.MovementCounter("Ralph")}/{native[at+7]}, Z={owner.ZFixed}/{unchecked((short)native.Word(at+14))}, speedZ={owner.SpeedZ}/{unchecked((short)native.Word(at+0x14))}, publication={!observing}).");
                        if(native[0xcba0]!=0)
                        {
                            if(hold==0)hold=3;
                            if(--hold==0){_dialogue.Close();native[0xcba0]=0;}
                        }
                        if(injectThisUpdate){native[writer]=0;injectThisUpdate=false;}
                        Seed();
                    });
                }
            }
            finally{_runtimeState.Changed-=Observe;}
            FailIf(native[at]!=0||moves!=47+Math.Abs(linkX-56)-1+67||late&&lateCount!=3||
                !_saveData.HasRoomFlag(1,0x97,0x40)||_player.CutsceneControlled||owner.MenusDisabled,
                "Ralph must complete47 approach moves, N-1 alignment moves and67 departure moves, then flag$40/music/input restoration.");
            StepGameplayUpdates(3,Vector2.Zero,batched:batched);
        }
    }

    private void ValidateRalphRaftonAlignmentRom()
    {
        foreach(var spec in new (int X,int Link)[]{(56,56),(56,57),(57,56),(56,80),(80,56),(0,255),(255,0)})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetGlobalFlag(0x15);LoadValidationRoom(1,0x97);
            var owner=_roomEvents.Get<RalphAfterRaftonEvent>();var actor=_entities.Entities<RalphAfterRaftonCharacter>().Single();
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity=>ReferenceEquals(entity.Node,actor)));
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(spec.Link+0.75f,104.25f));
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            F1Set(owner,"_precisePosition",new Vector2(spec.X+0.25f,104.5f));actor.Position=new(spec.X,104);
            F1Set(owner,"_substate",6);F1Set(owner,"_counter",1);F1Set(owner,"_speed",0x28);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,spec.Link,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x37;native[at+2]=3;native[at+11]=56;native[at+13]=56;native.AdvanceInteractions();
            native.Word(at+10,0x6880);native.Word(at+12,spec.X*256+0x40);native[at+5]=6;native[at+6]=1;native[at+16]=0x28;native[0xcd00]=0;
            int updates=0,moves=0;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    int previous=native.Word(at+12);native.AdvanceInteractions(_entities.FrameCounter);updates++;
                    if(previous!=native.Word(at+12))moves++;
                    FailIf(owner.Substate!=native[at+5]||owner.Substate<8&&owner.Counter!=native[at+6]||
                        SomariaPrivate<Vector2>(owner,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        actor.CurrentAnimationCounter!=native[at+0x20]||
                        Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                        $"Ralph alignment${spec.X:x2}->${spec.Link:x2} update{updates}: native unsigned difference/counter/fractions/animation/publication differs.");
                    Seed();
                });
            Seed();Step(1);
            if(spec is(56,80))
            {
                Step(1);
                _dialogue.ShowMessage("Ralph's ordinary alignment pause.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
                _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            }
            while(native[at+5]!=8&&updates<280)Step(Math.Min(3,(int)native[at+6]));
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");
            FailIf(owner.Substate!=8||moves!=Math.Max(0,Math.Abs(spec.Link-spec.X)-1)||owner.CurrentCommandIndex!=0||
                runner.Counter!=0||runner.CurrentCommandUpdates!=0,
                "Ralph zero/one/unsigned-ff alignment must install the script without running its first wait.");
            owner.Cancel();_entities.Clear();
            if(spec is(56,80))
            {
                for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
                StepGameplayUpdates(3,Vector2.Zero,batched:batched);
                FailIf(owner.HasState||_player.CutsceneControlled||owner.MenusDisabled||_saveData.HasRoomFlag(1,0x97,0x40)||
                    Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=(byte)(0xa5+i)),
                    "Cancelling Ralph's alignment/script handoff must stop publication and release input without flag$40.");
            }
        }
    }
}
