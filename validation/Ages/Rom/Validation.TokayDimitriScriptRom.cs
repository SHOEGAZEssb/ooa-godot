using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTokayDimitriScriptRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool heard in new[]{false,true})
        foreach(int seeds in new[]{-1,0,1,0x20})
        foreach(int choice in seeds>0?new[]{0,1}:new[]{0})
        foreach(int free in new[]{0,1,2})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_saveData.WriteWramByte(AgesWramAddress.wDimitriState,heard?(byte)1:(byte)0);
            _inventory.GiveTreasure(TreasureId.Essence,2);_inventory.GiveTreasure(TreasureId.EmberSeeds,Math.Max(0,seeds));
            if(seeds<0)_inventory.LoseTreasure(TreasureId.EmberSeeds);
            _inventory.SetScriptedEquippedItems(0,0);LoadValidationRoom(0,0xaa);_player.WarpTo(new(80,104));
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(e=>e is not (TokayNpcRoomEntity or DimitriPresetSpawnerRoomEntity or DimitriRescueScriptRoomEntity)).ToArray())_entities.RetireEntity(other);
            var slots=_entities.EntityAdapters<TokayNpcRoomEntity>().OrderBy(s=>s.Npc.Record.SubId).ToArray();
            var controller=_entities.EntityAdapters<DimitriRescueScriptRoomEntity>().Single();
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            foreach(var pair in InteractionAdmissionPool)
            {
                int at=0xd040+pair.Value*256;native[at]=1;
                (int id,int sub)=pair.Key switch{TokayNpcRoomEntity n=>(0x48,n.Npc.Record.SubId),DimitriPresetSpawnerRoomEntity=>(0x67,3),DimitriRescueScriptRoomEntity=>(0x71,7),_=>throw new InvalidOperationException()};
                native[at+1]=(byte)id;native[at+2]=(byte)sub;
                native[at+11]=(byte)pair.Key.Node.Position.Y;native[at+13]=(byte)pair.Key.Node.Position.X;
            }
            FailIf(slots.Length!=2||_entities.InteractionSlot(slots[0].Node)!=2||_entities.InteractionSlot(slots[1].Node)!=3||_entities.InteractionSlot(controller.Node)!=5,"Dimitri's imported $48/$48/$67/$71 prefix must retain native slots $d2-$d5.");
            int updates=0;var texts=new HashSet<int>();string source=$"Dimitri scripts/heard{heard}/seeds{seeds}/choice{choice}/free{free}/batch{batched}";
            void Step(int n)=>StepGameplayUpdates(n,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                foreach(var slot in slots)
                {
                    int at=0xd040+(slot.Npc.Record.SubId==0x0f?2:3)*256;
                    FailIf(slot.Npc.Active!=(native[at]!=0)||slot.Npc.Active&&
                        (slot.Script!.NativeCounter1!=native[at+6]||slot.Script.MovementCounter!=native[at+7]||
                            slot.Npc.Position!=new Vector2(native[at+13],native[at+11])||slot.Npc.NativeAngle!=native[at+9]),
                        $"{source} update{updates}/Tokay${slot.Npc.Record.SubId:x2}: native counters, position, angle or lifetime differs (cmd{slot.Script!.CurrentCommandIndex}, c1{slot.Script.NativeCounter1}/{native[at+6]}, c2{slot.Script.MovementCounter}/{native[at+7]}, angle{slot.Npc.NativeAngle:x2}/{native[at+9]:x2}).");
                }
                FailIf(CompanionRuntimeState.ReadScriptSignals(_runtimeState)!=native[0xd13e]||_saveData.ReadWramByte(AgesWramAddress.wDimitriState)!=native[AgesWramAddress.wDimitriState]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02]||_inventory.EmberSeeds!=native[0xc6b9],source+$" update{updates}: source signals, masks, state or BCD seed count differs.");
                if(_dialogue.IsOpen)
                {
                    int id=native.Word(0xcba2)-0x400;texts.Add(id);
                    string expected=id==0x2100?new DimitriDatabase().Text(id):new TokayInteractionDatabase().Text(id);
                    FailIf(_dialogue.CurrentMessage!=DialogueBox.PlainText(expected),source+$" update{updates}: text TX_${id:x4} differs.");
                    if(id==0x0a20)FailIf(!_dialogue.ChoiceActive||!_dialogue.CurrentMessage.Contains("Will you trade"),"Unterminated TX_0a20 must include TX_0a21's source choice.");
                }
                var children=_entities.Entities<TokayRescueEmberRoomEntity>();
                int count=Enumerable.Range(2,14).Count(s=>native[0xd040+s*256]!=0&&native[0xd041+s*256]==0x8f);
                FailIf(children.Count!=count,source+$" update{updates}: checked $8f allocation differs ({children.Count}/{count}).");
                foreach(var child in children)
                {
                    int at=0xd040+_entities.InteractionSlot(child)*256;
                    FailIf(native[at+1]!=0x8f||native[at+4]!=child.Phase||child.ZFixed!=(short)native.Word(at+14)||child.Counter!=native[at+6],source+$" update{updates}: staggered $8f state, counter or gravity differs.");
                }
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            });
            void Close(){if(_dialogue.ChoiceActive){_dialogue.SubmitChoiceForValidation(choice);native[0xcba5]=(byte)choice;}else _dialogue.Close();native[0xcba0]=0;}
            for(int i=0;i<100;i++)
            {
                if(_dialogue.IsOpen)Close();Step(1);
                if(!_dialogue.IsOpen&&slots[0].Script!.CurrentCommandIndex==8&&(_saveData.ReadWramByte(AgesWramAddress.wDimitriState)&1)!=0)break;
            }
            FailIf(!_saveData.ReadWramByte(AgesWramAddress.wDimitriState).Equals((byte)1),source+": independent introduction did not finish.");
            if(!heard)FailIf(!texts.IsSupersetOf(new[]{0x0a1d,0x0a1e,0x2100}),source+": missing original introduction text.");
            while(14-InteractionAdmissionPool.Count>free)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);_entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int at=0xd040+_entities.InteractionSlot(puff)*256;native[at]=1;native[at+1]=0x48;native[at+2]=0x1b;native[at+4]=1;
            }
            FailIf(!slots[0].Script!.TryInteract(new(slots[0].Npc,null),_player),source+": cannot queue first Tokay A.");native[0xd271]=1;
            bool accepted=seeds>0&&choice==0;int held=0;
            for(int i=0;i<400;i++)
            {
                if(_dialogue.IsOpen&&++held>=4){Close();held=0;}else if(!_dialogue.IsOpen)held=0;
                Step(1);
                if(accepted ? slots.All(s=>!s.Npc.Active) : i>5&&!_dialogue.IsOpen&&slots[0].Script!.CurrentCommandIndex==8)break;
            }
            FailIf(!texts.Contains(0x0a1f)||_runtimeState.ReadWramByte(0xcc8a)!=0||
                (_saveData.ReadWramByte(AgesWramAddress.wDimitriState)&2)!= (accepted?2:0)||
                _inventory.EmberSeeds!=(accepted?seeds==0x20?0x19:0:Math.Max(seeds,0)),source+": script result, BCD consumption or release did not complete.");
            if(accepted)FailIf(!texts.IsSupersetOf(new[]{0x0a20,0x0a23,0x0a24,0x0a25}),source+": accepted trade omitted source text.");
            else if(seeds>0)FailIf(!texts.Contains(0x0a22),source+": rejected trade omitted TX_0a22.");
            _entities.Clear();
        }
    }
}
