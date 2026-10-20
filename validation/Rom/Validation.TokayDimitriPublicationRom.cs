using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayDimitriPublicationRom()
    {
        foreach(int scroll in new[]{0,4,8,0x0e})
        foreach(int holdLength in new[]{3,20,36})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_saveData.WriteWramByte(WramAddress.wDimitriState,1);
            _inventory.GiveTreasure(TreasureId.Essence,2);_inventory.SetScriptedEquippedItems(0,0);
            LoadValidationRoom(0,0xaa);_player.WarpTo(new(80,104));
            StepGameplayUpdates(3,Vector2.Zero,batched:batched);
            var first=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x0f);
            var second=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x10);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            int a=0xd040+_entities.InteractionSlot(first.Node)*256,b=0xd040+_entities.InteractionSlot(second.Node)*256;
            foreach(var (slot,at,sub) in new[]{(first,a,0x0f),(second,b,0x10)})
            {
                native[at]=1;native[at+1]=0x48;native[at+2]=(byte)sub;
                native[at+11]=(byte)slot.Node.Position.Y;native[at+13]=(byte)slot.Node.Position.X;
            }
            native.AdvanceInteractions();
            // Stage only the native move-entry inputs. Both implementations
            // execute the actual move opcode, its counters and continuations.
            native[0xcba0]=0;native[0xd13e]=0x0c;native[0xcc8a]=0x81;native[0xcc02]=0x80;
            CompanionRuntimeState.OrScriptSignals(_runtimeState,0x0c);
            _runtimeState.SetWramByte(0xcc8a,0x81);_runtimeState.SetWramByte(0xcc02,0x80);
            foreach(var (slot,at,pc,number) in new[]{(first,a,0x61d7,1),(second,b,0x6219,2)})
            {
                Vector2 position=slot.Node.Position+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
                F1Set(slot.Script!,"_precisePosition",position);((TokayCharacter)slot.Node).SetStatePosition(position.Floor());
                var commands=CutsceneCommandCatalog.Load($"res://assets/oracle/cutscenes/tokay_dimitri_{number}_commands.tsv");
                var runner=(CutsceneCommandRunner)typeof(NpcInteractionCommandHost).GetField("_runner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(slot.Script)!;
                runner.Start(commands,commands.Single(c=>c is CutsceneMoveCommand).Source.CommandIndex);
                runner.SetNativeActorSpeed("Tokay",0x50);
                native.Word(at+10,(int)(position.Y*256));native.Word(at+12,(int)(position.X*256));
                native[at+6]=native[at+7]=0;native[at+16]=0x50;native.Word(at+0x18,pc);
            }
            byte[] observed=new byte[4],expected=new byte[4];bool armed=false,externalHold=false;int updates=0;
            void Capture()
            {
                if(!armed||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);armed=false;
            }
            _runtimeState.Changed+=Capture;
            var before=new PartMovementScratchProbe(()=>
            {
                armed=false;for(int i=0;i<4;i++){observed[i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}armed=true;
            });
            _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];
                foreach(var (slot,at) in new[]{(first,a),(second,b)})
                {
                    var actor=(TokayCharacter)slot.Node;
                    FailIf(actor.Active!=(native[at]!=0)||actor.Active&&
                        (slot.Script!.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            actor.Position!=new Vector2(native[at+13],native[at+11])||
                            slot.Script.MovementCounter!=native[at+7]||actor.CurrentAnimationCounter!=native[at+0x20]),
                        $"Dimitri Tokay${actor.Record.SubId:x2}/scroll${scroll:x2}/hold{holdLength}/update{updates}: native motion, counters, animation or slot lifetime differs.");
                }
                FailIf(!observed.SequenceEqual(expected)||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||
                    _saveData.ReadWramByte(WramAddress.wDimitriState)!=native[WramAddress.wDimitriState],
                    $"Dimitri Tokay update{updates}: original movement publication, raw masks or state byte differs.");
                if(!externalHold)FailIf(_dialogue.IsOpen,"Staged departures must not create another conversation.");
                armed=false;
            });
            Step(4);externalHold=true;_dialogue.ShowMessage("Tokay departure hold.",104);native[0xcba0]=1;Step(3);
            _dialogue.Close();native[0xcba0]=0;externalHold=false;
            _runtimeState.SetWramByte(0xcc8a,0x83);native[0xcc8a]=0x83;Step(3);
            _runtimeState.SetWramByte(0xcc8a,0x81);native[0xcc8a]=0x81;
            _runtimeState.SetWramByte(0xcd00,(byte)scroll);native[0xcd00]=(byte)scroll;Step(holdLength);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(35);
            FailIf(first.Npc.Active||second.Npc.Active||_runtimeState.ReadWramByte(0xcc8a)!=0,
                "Both independent Tokay source continuations must delete and release input.");
            _runtimeState.Changed-=Capture;_entities.Clear();
        }
    }
}
