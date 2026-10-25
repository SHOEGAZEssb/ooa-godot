using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateWildTokayManagerScriptRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int sub in new[]{0x0d,0x19})
        foreach(int mode in sub==0x0d?new[]{0,1,2,3,4}:new[]{0,1,2,3})
        foreach(int free in new[]{0,1})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.WriteWramByte(AgesWramAddress.wUnappraisedRingsEnd,0x34);save.WriteWramByte(0xc601,0x12);
            InitializeTransientSession(save);var data=new WildTokayGameDatabase();
            _inventory.SetScriptedEquippedItems(0,0);_inventory.GiveTreasure(TreasureId.Bracelet,1);
            if(sub==0x0d&&mode==0)_inventory.LoseTreasure(TreasureId.Bracelet);
            _inventory.AddRupees(mode==2?0:20);
            if(sub==0x19&&mode!=0)_saveData.SetGlobalFlag(data.FinishedGameFlag);
            if(sub==0x19&&mode>=2)_saveData.SetGlobalFlag(data.BeganSecretFlag);
            if(sub==0x19&&mode==3)_saveData.SetGlobalFlag(data.DoneSecretFlag);
            int room=sub==0x0d?0xde:0xe5;LoadValidationRoom(2,room);_player.WarpTo(new(80,80));
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==sub);F1Retain(slot);
            var game=_roomEvents.Get<WildTokayGameEvent>();var host=SomariaPrivate<WildTokayManagerScriptHost>(game,"_manager");
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,80);
            int at=0xd040+_entities.InteractionSlot(slot.Npc)*256;native[at]=1;native[at+1]=0x48;native[at+2]=(byte)sub;native[at+11]=(byte)slot.Npc.Position.Y;native[at+13]=(byte)slot.Npc.Position.X;
            int updates=0;string source=$"Wild manager${sub:x2}/mode{mode}/free{free}/batch{batched}";var shown=new HashSet<int>();
            void Step(int n)=>StepGameplayUpdates(n,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                try{native.AdvanceInteractions(_entities.FrameCounter);}catch(Exception error){throw new InvalidOperationException(source+$" update{updates+1}/cmd{host.CurrentCommandIndex}",error);}updates++;
                FailIf(game.Counter!=native[at+6]||_runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02]||
                    _runtimeState.ReadWramByte(0xcfdd)!=native[0xcfdd]||host.ReadMemory("CannotAfford")!=native[at+0x3d]||_inventory.Rupees!=((native[0xc6ad]>>4)*10+(native[0xc6ad]&15)+(native[0xc6ae]&15)*100),
                    source+$" update{updates}: native counter, masks, prize, rupee snapshot or consumption differs (cmd{host.CurrentCommandIndex}, counter{game.Counter}/{native[at+6]}, raw{_runtimeState.ReadWramByte(0xcc8a):x2}/{native[0xcc8a]:x2}, rupees{_inventory.Rupees}, bytes{native[0xc6ad]:x2}/{native[0xc6ae]:x2}).");
                if(_dialogue.IsOpen)
                {
                    int id=native.Word(0xcba2)-0x400;shown.Add(id);
                    string expected=DialogueBox.PlainText(new TokayInteractionDatabase().Text(id));
                    if(id==0x0a53)expected=expected.Replace("\\secret1",new LinkedGameNpcDatabase().GenerateSecret(0x15,_saveData),StringComparison.Ordinal);
                    FailIf(_dialogue.CurrentMessage!=expected,source+$" update{updates}: original TX_${id:x4} differs.");
                }
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            });
            Step(4);
            while(14-InteractionAdmissionPool.Count>free)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);_entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int p=0xd040+_entities.InteractionSlot(puff)*256;native[p]=1;native[p+1]=0x48;native[p+2]=0x1b;native[p+4]=1;
            }
            FailIf(!game.TryInteractNpc(slot.Npc),source+": original A list not ready.");native[at+0x31]=1;Step(1);
            int held=0;bool ending=sub==0x0d&&mode>=3||sub==0x19&&mode==2;
            for(int i=0;i<180;i++)
            {
                if(_dialogue.IsOpen&&++held==3)
                {
                    if(_dialogue.ChoiceActive)
                    {
                        int choice=sub==0x0d?(mode==1||mode==3&&native.Word(0xcba2)-0x400==0x0a14?1:0):mode==1?1:0;
                        _dialogue.SubmitChoiceForValidation(choice);native[0xcba5]=(byte)choice;
                    }
                    else _dialogue.Close();native[0xcba0]=0;held=0;
                }
                else if(!_dialogue.IsOpen)held=0;
                Step(1);
                if(_transitions.NativePaletteFadeActive||i>5&&!_dialogue.IsOpen&&_runtimeState.ReadWramByte(0xcc8a)==0)break;
            }
            // A full pool leaves scriptend live, retrying $70 without fading.
            if(ending&&free==0)
            {
                FailIf(_transitions.NativePaletteFadeActive||_entities.EntityAdapters<WildTokayControllerRoomEntity>().Any(),source+": failed $70 allocation must not begin a fade.");
                var filler=InteractionAdmissionPool.First(p=>p.Key.Node is PuzzlePuffEffect);int page=filler.Value;
                _entities.RetireEntity(filler.Key);for(int i=0;i<0x40;i++)native[0xd040+page*256+i]=0;Step(1);
            }
            if(ending)FailIf(!_transitions.NativePaletteFadeActive||!_entities.EntityAdapters<WildTokayControllerRoomEntity>().Any(),source+": successful $70 allocation must start the fade while the manager still occupies its slot.");
            _roomEvents.Get<WildTokayGameEvent>().Cancel();_dialogue.Close();_entities.Clear();
        }
    }
}
