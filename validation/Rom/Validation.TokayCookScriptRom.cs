using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayCookScriptRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int free in new[]{0,1})
        foreach(int trade in new[]{0,1,2,3})
        foreach(int choice in trade==2 ? new[]{0,1} : new[]{0})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(TreasureId.TradeItem,trade);
            if(trade==0)_inventory.LoseTreasure(TreasureId.TradeItem);
            _inventory.SetScriptedEquippedItems(0,0);_saveData.SetRoomFlag(2,0x3f,0x20,false);
            LoadValidationRoom(2,0x3f);
            var cooking=_roomEvents.Get<TokayCookEvent>();
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==5);
            var actor=(TokayCharacter)slot.Npc;F1Retain(slot);_player.WarpTo(new(72,88));
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,72,88);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=5;
            native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            while(14-InteractionAdmissionPool.Count>free)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int occupied=0xd040+_entities.InteractionSlot(puff)*256;
                // A state-one museum statue is inert while inPresent=$00.
                // An uninitialized debris animation eventually expires in
                // this long six-jump conversation and cannot hold capacity.
                native[occupied]=1;native[occupied+1]=0x48;native[occupied+2]=0x1b;native[occupied+4]=1;
            }
            int updates=0;var shown=new HashSet<int>();
            string source=$"Cook$48:$05/trade{trade}/choice{choice}/free{free}/batched{batched}";
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                FailIf(cooking.NativeCounter1!=native[at+6]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02]||
                    _inventory.TradeItem!=native[0xc6c0]||_saveData.GetRoomFlags(2,0x3f)!=native[0xc73f]||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||
                    SomariaPrivate<bool>(cooking,"_awayFromStart")!=(native[at+0x3e]!=0),
                    source+$" update{updates}: counter, control, inventory, flag or native jump handshake differs (cmd{cooking.CurrentCommandIndex}, counter{cooking.NativeCounter1}/{native[at+6]}, flags{_saveData.GetRoomFlags(2,0x3f):x2}/{native[0xc73f]:x2}).");
                if(_dialogue.IsOpen)
                {
                    int text=native.Word(0xcba2)-0x400;
                    // The get-item textbox belongs to the common treasure
                    // owner; compare the cook's own text at script boundaries.
                    if(text>=0x0a00&&text<=0x0a09)
                    {
                        FailIf(_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(text)),source+$" update{updates}: original cook text differs.");shown.Add(text);
                    }
                    else FailIf(!_inventory.HasTreasure(TreasureId.TradeItem)||_inventory.TradeItem!=3,source+": unexpected native text before the reward.");
                }
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            });
            Step(3);FailIf(!cooking.TryInteractNpc(actor),source+": cannot queue A.");native[at+0x31]=1;Step(1);
            int held=0;
            for(int i=0;i<1200;i++)
            {
                if(_dialogue.IsOpen)
                {
                    if(++held>12)
                    {
                        if(_dialogue.ChoiceActive){_dialogue.SubmitChoiceForValidation(choice);native[0xcba5]=(byte)choice;}
                        else _dialogue.Close();
                        native[0xcba0]=0;held=0;
                    }
                }
                else held=0;
                Step(1);
                if(i>10&&cooking.CurrentCommandIndex==1&&!_dialogue.IsOpen&&_runtimeState.ReadWramByte(0xcc8a)==0)break;
            }
            bool awarded=trade==2&&choice==0&&free!=0;
            FailIf(!shown.Contains(0x0a00)||_runtimeState.ReadWramByte(0xcc8a)!=0||
                _inventory.TradeItem!=(awarded?3:trade)||_saveData.HasRoomFlag(2,0x3f,0x20)!=awarded||
                !shown.Contains(trade==2 ? choice==0?0x0a06:0x0a08 : 0x0a09),
                source+": original branch, checked reward result or input release did not complete.");
            _entities.Clear();
        }
    }
}
