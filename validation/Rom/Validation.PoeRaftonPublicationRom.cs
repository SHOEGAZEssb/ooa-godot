using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePoePublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool cancel in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(0,0x7c,0x40);LoadValidationRoom(2,0x2e);
            var owner=_roomEvents.Get<PoeEvent>();var actor=_entities.Entities<PoeCharacter>().Single();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x59;native[at+3]=1;native[at+11]=32;native[at+13]=80;native.AdvanceInteractions();
            // State zero actually loads scriptHelp.poeScript into$c300.
            // Start after TX_0b01/wait30/var3f=1, at its original setspeed.
            FailIf(native[0xc32c]!=0x8b||native[0xc32d]!=0x28,
                "Poe's original copied SPEED_100 command changed at$c32c.");
            native.Word(at+0x18,0xc32c);native[at+0x3f]=1;native[at+6]=native[at+7]=0;
            Vector2 initial=new(248.25f,248.5f);F1Set(owner,"_precisePosition",initial);actor.Position=initial.Floor();actor.SetNoFace(true);
            native.Word(at+10,0xf880);native.Word(at+12,0xf840);
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");runner.Start(owner.Database.Commands,16);
            var moves=owner.Database.Commands.OfType<CutsceneApplySpeedCommand>().ToArray();
            FailIf(moves.Length!=2||moves[0].Counter!=0x49||moves[1].Counter!=0x39,
                "Poe's tomb departure must retain the two original$49/$39 counters.");
            int updates=0,movementUpdates=0,publications=0;bool observing=false,openDuringPass=false;
            byte[] observed=new byte[4];
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {if(openDuringPass)_dialogue.ShowMessage("Earlier interaction opens text.",_player.Position.Y);}));
            void Observe()
            {
                if(!observing||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                observing=false;publications++;
            }
            void Seed(){observing=false;for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}observing=true;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int x=native.Word(at+12),y=native.Word(at+10);native.AdvanceInteractions(_entities.FrameCounter);updates++;
                bool moved=native[at]!=0&&(x!=native.Word(at+12)||y!=native.Word(at+10));if(moved)movementUpdates++;
                FailIf(actor.Active!=(native[at]!=0)||actor.Active&&(
                    SomariaPrivate<Vector2>(owner,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.CurrentAnimationCounter!=native[at+0x20]||runner.MovementCounter("Poe")!=native[at+7])||
                    moved&&(observing||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]))||!moved&&!observing,
                    $"Poe update{updates}: native position/counter/animation/admission/publication differs (command={owner.CurrentCommandIndex}, animation={actor.CurrentAnimationCounter}/{native[at+0x20]}, publication={!observing}).");
                Seed();
            });
            _runtimeState.Changed+=Observe;
            try
            {
                Seed();Step(5);
                if(!cancel)
                {
                    _dialogue.ShowMessage("Ordinary Poe entry hold.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                    _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                    _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
                    // Category admission precedes this earlier actor's text.
                    // Its native showtext stream and the runtime probe have
                    // the same live script gate; Poe still animates this pass.
                    int earlier=Enumerable.Range(0,16).Select(i=>0xd040+i*256).First(i=>i!=at);
                    native[earlier]=1;native[earlier+1]=0x3c;native[earlier+2]=1;native[earlier+4]=1;
                    native[0xc180]=0x98;native[0xc181]=0x57;native[0xc182]=3;native[0xc183]=0xf0;native.Word(earlier+0x18,0xc180);
                    openDuringPass=true;Step(1);openDuringPass=false;native[earlier]=0;
                    FailIf(native[0xcba0]==0||!_entities.InitializedInteractionUpdatesAllowed,
                        "Earlier native text must block Poe's script while retaining this pass's admitted animation.");
                    Step(2);_dialogue.Close();native[0xcba0]=0;
                    while(runner.CurrentCommand?.Source.CommandIndex is not 6 && updates<180)Step(1);
                    FailIf(movementUpdates!=128||publications!=128||runner.MovementCounter("Poe")!=0,
                        "Poe must execute72 downward and56 rightward moves before the original disappearance wait.");
                    Step(2);
                }
            }
            finally{_runtimeState.Changed-=Observe;}
            owner.Cancel();_entities.Clear();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(owner.HasState||_player.CutsceneControlled,"Poe cancellation must stop the original stream and release input.");
        }
    }

    private void ValidateRaftonPublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool cancel in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_inventory.GiveTreasure(0x54,0);LoadValidationRoom(2,0x1e);
            var owner=_roomEvents.Get<RaftonEvent>();var actor=_entities.Entities<RaftonCharacter>().Single();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,24));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,24);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x69;native[at+11]=64;native[at+13]=80;native.AdvanceInteractions();
            FailIf(native[at+0x38]!=4||owner.Behaviour!=4||owner.Database.RightCommands.Any(c=>c is CutsceneMoveCommand or CutsceneApplySpeedCommand),
                "Rafton's departure belongs only to the Island Chart left-room stream.");
            // Native moveright$40 at0c:$7179 and the real flag26/enable/end
            // tail. No synthetic post-motion wait changes counter1/counter2.
            native.Word(at+0x18,0x7179);native[at+16]=0x28;native.Word(at+10,0x4080);native.Word(at+12,0xf840);
            F1Set(owner,"_precisePosition",new Vector2(248.25f,64.5f));actor.Position=new(248,64);
            var runner=(CutsceneCommandRunner)typeof(InteractiveInfiniteScriptHost<RaftonCharacter>).GetField("_runner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
            runner.Start(owner.Database.LeftCommands,45);runner.SetInitialMotionRegisters("Rafton",0x28,8);
            FailIf(owner.Database.LeftCommands[45] is not CutsceneMoveCommand{Angle:8,Counter:0x40},"Rafton source moveright$40 changed.");
            int updates=0,moves=0;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previous=native.Word(at+12);native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(native[at]!=0&&previous!=native.Word(at+12))moves++;
                FailIf(actor.Active!=(native[at]!=0)||actor.Active&&(
                    SomariaPrivate<Vector2>(owner,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.CurrentAnimationCounter!=native[at+0x20]||runner.MovementCounter("Rafton")!=native[at+7]||
                    actor.ZIndex!=ObjectDrawPriority.FromVisible(native[at+0x1a]))||
                    _saveData.HasGlobalFlag(0x26)!=((native[WramAddress.wGlobalFlags+4]&0x40)!=0)||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Rafton update{updates}: native move/counter/animation/priority/flag/publication differs (XY={SomariaPrivate<Vector2>(owner,"_precisePosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, animation={actor.CurrentAnimationCounter}/{native[at+0x20]}, priority={actor.ZIndex}/{ObjectDrawPriority.FromVisible(native[at+0x1a])}, scratch={string.Join(',',Enumerable.Range(0,4).Select(i=>_runtimeState.ReadWramByte(0xcec0+i).ToString("x2")))}/{native.Word(0xcec0):x4},{native.Word(0xcec2):x4}).");
                Seed();
            });
            Seed();Step(4);
            if(!cancel)
            {
                _dialogue.ShowMessage("Ordinary Rafton entry hold.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
                while(actor.Active&&updates<100)Step(1);
                FailIf(actor.Active||moves!=63||!_saveData.HasGlobalFlag(0x26)||_player.CutsceneControlled,
                    "Rafton must publish63 moves and set flag$26/release input/delete on the zero update.");
                Step(2);
            }
            owner.Cancel();_entities.Clear();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(_player.CutsceneControlled||cancel&&_saveData.HasGlobalFlag(0x26),"Cancelling Rafton must release input without inventing the room-change flag.");
        }
    }
}
