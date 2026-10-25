using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePatchStaircasePublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool cancel in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_runtimeState.SetWramByte(0xcfd2,0);LoadValidationRoom(3,0xbe);
            var owner=_roomEvents.Get<PatchEvent>();var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record is{Id:0x94,SubId:0});
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x94;native[at+11]=40;native[at+13]=88;native.AdvanceInteractions();
            // patch_subid00 state1's script has ended after landing. Both
            // handlers now enter the original staircase stream, not a test VM.
            Vector2 initial=new(8.25f,8.5f);F1Set(owner,"_patchPosition",initial);actor.Position=initial.Floor();
            F1Set(owner,"<State>k__BackingField",2);
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");
            runner.Start(owner.Database.Commands,owner.Database.Entry("upstairsMoveToStaircaseScript"));runner.SetInitialMotionRegisters("Patch",0x28,0);
            var moves=owner.Database.Commands.Skip(owner.Database.Entry("upstairsMoveToStaircaseScript")).OfType<CutsceneMoveCommand>().ToArray();
            FailIf(moves.Length!=2||moves[0].Angle!=0||moves[0].Counter!=0x14||moves[1].Angle!=8||moves[1].Counter!=0x32,
                "Patch stairs source motion must remain up$14/right$32 at SPEED_100.");
            native[at+4]=2;native.Word(at+0x18,0x788d);native.Word(at+10,0x0880);native.Word(at+12,0x0840);
            int updates=0,movementUpdates=0,publications=0;bool observing=false;byte[] observed=new byte[4];
            void Observe()
            {
                // Both cardinal velocities end in X-high zero, unlike the
                // seeded$a8. Capture all four bytes before later warp reuse.
                if(!observing||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                observing=false;publications++;
            }
            void Seed(){observing=false;for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}observing=true;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previousX=native.Word(at+12),previousY=native.Word(at+10);native.AdvanceInteractions(_entities.FrameCounter);updates++;
                bool moved=native[at]!=0&&(previousX!=native.Word(at+12)||previousY!=native.Word(at+10));if(moved)movementUpdates++;
                FailIf(actor.Active!=(native[at]!=0)||actor.Active&&(
                    SomariaPrivate<Vector2>(owner,"_patchPosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||actor.CurrentAnimationCounter!=native[at+0x20]||
                    runner.MovementCounter("Patch")!=native[at+7])||
                    _runtimeState.ReadWramByte(0xcfd2)!=native[0xcfd2]||
                    moved&&(observing||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]))||!moved&&!observing||
                    Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Patch stairs update{updates}: native movement/counter/animation/signal/publication differs (XY={SomariaPrivate<Vector2>(owner,"_patchPosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, counter={runner.MovementCounter("Patch")}/{native[at+7]}, animation={actor.CurrentAnimationCounter}/{native[at+0x20]}, signal={_runtimeState.ReadWramByte(0xcfd2)}/{native[0xcfd2]}, observed={!observing}).");
                Seed();
            });
            _runtimeState.Changed+=Observe;
            try
            {
                Seed();Step(4);
                if(!cancel)
                {
                    _dialogue.ShowMessage("Patch native script hold.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                    _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                    _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
                    while(actor.Active&&updates<160)Step(1);
                    FailIf(actor.Active||movementUpdates!=68||publications!=68||_runtimeState.ReadWramByte(0xcfd2)!=1||_player.CutsceneControlled,
                        "Patch stairs must execute19+49 source moves, publish downstairs01 and release input on script completion.");
                    Step(2);
                }
            }
            finally{_runtimeState.Changed-=Observe;}
            owner.Cancel();_entities.Clear();
            for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
            StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(_player.CutsceneControlled||owner.HasState||cancel&&_runtimeState.ReadWramByte(0xcfd2)!=0||
                Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=(byte)(0xa5+i)),
                "Patch staircase cancellation/retirement must stop movement without inventing completion.");
        }
    }
}
