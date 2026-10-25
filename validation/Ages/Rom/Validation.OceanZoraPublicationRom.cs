using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateOceanZoraPublicationRom()
    {
        var data=new OceanInteractionDatabase();
        FailIf(data.Scale.OfType<CutsceneMoveCommand>().Count()!=2||data.StormGuard.OfType<CutsceneMoveCommand>().Count()!=4,
            "Ocean Zora source inventory must contain two scale and four storm-guard movement sites.");
        var cases=new (bool Scale,bool Linked,int Index,int Angle,int Counter)[]{(true,false,9,0,0x20),
            (true,false,14,16,0x80),(false,false,15,24,0x18),(false,false,17,0,0x30),
            (false,true,19,16,0x10),(false,true,21,8,0x40)};
        foreach(var spec in cases)
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(spec.Linked);
            _inventory.GiveTreasure(TreasureId.Essence,6);
            if(!spec.Scale)_inventory.GiveTreasure(TreasureId.ZoraScale,0);
            LoadValidationRoom(spec.Scale?2:spec.Linked?2:3,spec.Scale?0xa0:spec.Linked?0xd7:0xd6);
            var owner=_roomEvents.Get<OceanZoraEvent>();var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0xab&&n.Record.SubId==(spec.Scale?0x10:spec.Linked?0x12:0x11));
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity=>ReferenceEquals(entity.Node,actor)));
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            F1Set(owner,"_position",new Vector2(8.25f,8.5f));F1Set(owner,"_walkingAway",true);actor.Position=new(8,8);
            var runner=(CutsceneCommandRunner)typeof(InteractiveInfiniteScriptHost<NpcCharacter>).GetField("_runner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
            CutsceneCommand original=(spec.Scale?data.Scale:data.StormGuard)[spec.Index];
            FailIf(original is not CutsceneMoveCommand move||move.Angle!=spec.Angle||move.Counter!=spec.Counter,
                $"Ocean Zora source motion index{spec.Index} changed.");
            runner.Start([original with{Source=original.Source with{CommandIndex=0}},
                new CutsceneWaitCommand(original.Source with{CommandIndex=1,Opcode="wait"},255),
                new CutsceneEndCommand(original.Source with{CommandIndex=2,Opcode="scriptend"})]);
            int speed=spec.Scale?0x28:0x50;runner.SetInitialMotionRegisters("Zora",speed,spec.Angle);
            FailIf((spec.Scale?data.Scale:data.StormGuard).Take(spec.Index).OfType<CutsceneSetSpeedCommand>().Last().Speed!=speed,
                $"Ocean Zora source line{original.Source.SourceLine}: incoming speed changed.");
            var native=new ObjectMotionRom();const int at=ObjectMotionRom.Object+0x40;
            native.Word(at+10,0x0880);native.Word(at+12,0x0840);native[at+9]=(byte)spec.Angle;native[at+16]=(byte)speed;
            var animation=new ObjectAnimationRom(0,0xab);animation.Set(spec.Angle/8);
            int eligible=0,moves=0;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count,bool frozen=false)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                if(!frozen)
                {
                    if(eligible>0&&eligible<spec.Counter){native.Call(ObjectMotionRom.ApplySpeed,slot:0x40);moves++;}
                    animation.Advance();eligible++;
                }
                FailIf(SomariaPrivate<Vector2>(owner,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.CurrentAnimationCounter!=animation.Counter||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Ocean Zora source line{original.Source.SourceLine} eligible{eligible}: native motion/animation/admission/publication differs.");
                Seed();
            });
            Seed();Step(4);
            _dialogue.ShowMessage("Ordinary ocean Zora pause.",_player.Position.Y);Step(3,true);_dialogue.Close();
            _runtimeState.SetWramByte(0xcc8a,2);Step(3,true);_runtimeState.SetWramByte(0xcc8a,0);
            _runtimeState.SetWramByte(0xcd00,8);Step(3,true);_runtimeState.SetWramByte(0xcd00,1);
            Step(spec.Counter-4+2);
            FailIf(moves!=spec.Counter-1,"Ocean Zora must execute every source motion update and skip its zero update.");
            owner.Cancel();_entities.Clear();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(_player.CutsceneControlled,"Ocean Zora cancellation retained its control lease.");
        }
    }

    private void ValidateOceanZoraAlignmentRom()
    {
        foreach(int counter in new[]{0,1,16,255})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_inventory.GiveTreasure(TreasureId.Essence,6);LoadValidationRoom(2,0xa0);
            var owner=_roomEvents.Get<OceanZoraEvent>();var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record is{Id:0xab,SubId:0x10});
            var runner=(CutsceneCommandRunner)typeof(InteractiveInfiniteScriptHost<NpcCharacter>).GetField("_runner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity=>ReferenceEquals(entity.Node,actor)));
            _inventory.SetScriptedEquippedItems(0,0);
            Vector2 initial=new(8.25f,64.5f);int targetX=(8-counter)&255;
            F1Set(owner,"_position",initial);actor.Position=initial.Floor();
            // Declared original helper inputs. The separate approach fixture
            // proves reachability; wrapped X inputs deliberately isolate byte math.
            _player.WarpTo(new(targetX+0.75f,24));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,targetX,24);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0xab;native[at+2]=0x10;native[at+11]=64;native[at+13]=8;
            native.AdvanceInteractions(); // Real state0 admission, before compared script updates.
            native.Word(at+10,0x4080);native.Word(at+12,0x0840);native[0xcd00]=0;
            int updates=0,moves=0,publications=0;bool held=false,observing=false,openDuringPass=false;byte[] observed=new byte[4];
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {if(openDuringPass)_dialogue.ShowMessage("Earlier interaction opens text.",_player.Position.Y);}));
            // Observe completion of the helper's ordered four-byte write.
            // Left SPEED_100 ends in $cec3=$ff; the later warp query reuses
            // only $cec0. No production trace or alternate movement is used.
            void Observe()
            {
                if(!observing||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                observing=false;publications++;
            }
            _runtimeState.Changed+=Observe;
            void Seed(){observing=false;for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}observing=true;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previous=native.Word(at+12);native.AdvanceInteractions(_entities.FrameCounter);updates++;
                bool moved=previous!=native.Word(at+12);if(moved)moves++;
                FailIf(SomariaPrivate<Vector2>(owner,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (owner.CurrentCommandIndex==9?runner.MovementCounter("Zora"):SomariaPrivate<int>(owner,"_alignmentCounter"))!=native[at+7]||actor.CurrentAnimationCounter!=native[at+0x20]||
                    moved&&(observing||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]))||
                    !moved&&!observing||Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Ocean Zora alignment${counter:x2} update{updates}: native helper/counter/position/animation/publication differs (XY={SomariaPrivate<Vector2>(owner,"_position")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, counter={SomariaPrivate<int>(owner,"_alignmentCounter")}/{native[at+7]}, runner={owner.CurrentCommandIndex}:{runner.Counter}/{runner.CurrentCommandUpdates}, animation={actor.CurrentAnimationCounter}/{native[at+0x20]}, nativeState={native[at]}:{native[at+4]}, scratch={string.Join(',',Enumerable.Range(0,4).Select(i=>_runtimeState.ReadWramByte(0xcec0+i).ToString("x2")))}/{native.Word(0xcec0):x4},{native.Word(0xcec2):x4}).");
                Seed();
            });
            try
            {
                Seed();
                while(owner.CurrentCommandIndex<9&&updates<330)
                {
                    Step(1);
                    if(!held&&SomariaPrivate<int>(owner,"_alignmentCounter")>2)
                    {
                        held=true;
                        int writer=at-256;FailIf(writer<0xd040,"Alignment late writer needs an earlier source slot.");
                        native[writer]=1;native[writer+1]=0x3c;native[writer+2]=1;native[writer+4]=1;
                        native.Word(writer+0x18,0xc180);native[0xc180]=0x98;native[0xc181]=0x57;native[0xc182]=3;
                        openDuringPass=true;Step(1);openDuringPass=false;
                        FailIf(native[0xcba0]==0||!_entities.InitializedInteractionUpdatesAllowed,"Earlier native showtext must preserve admitted Zora animation while blocking its counter.");
                        Step(3);native[writer]=0;_dialogue.Close();native[0xcba0]=0;
                        _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                    }
                }
            }
            finally{_runtimeState.Changed-=Observe;}
            FailIf(owner.CurrentCommandIndex!=9||moves!=Math.Max(0,counter-1)||publications!=moves||counter>2&&!held,
                "Zora alignment must preserve unsigned distance, exact counter-zero yield and next move setup.");
            owner.Cancel();_entities.Clear();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
        }
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_inventory.GiveTreasure(TreasureId.Essence,6);LoadValidationRoom(2,0xa0);
            var owner=_roomEvents.Get<OceanZoraEvent>();var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record is{Id:0xab,SubId:0x10});
            _inventory.SetScriptedEquippedItems(0,0);
            // Original automatic Y gate and a one-pixel negative X difference
            // keep the real helper active at its unsigned$ff cancellation boundary.
            _player.WarpTo(new(actor.Position.X+1,24));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(0xcd00,0);_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            Vector2 initial=actor.Position;StepGameplayUpdates(42,Vector2.Zero,batched:batched);
            FailIf(SomariaPrivate<int>(owner,"_alignmentCounter")==0||actor.Position==initial,
                "Cancellation must interrupt positive movement in the real Zora alignment helper.");
            owner.Cancel();_entities.Clear();
            for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
            StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(owner.HasState||_player.CutsceneControlled||SomariaPrivate<int>(owner,"_alignmentCounter")!=0||
                Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=(byte)(0xa5+i)),
                "Mid-alignment cancellation must clear the native mover and control without later velocity publication.");
        }
    }
}
