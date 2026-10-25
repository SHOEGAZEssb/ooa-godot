using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateWildTokayResultRom()
    {
        foreach(bool batch in new[]{false,true})
        foreach(bool present in new[]{false,true})
        foreach(bool lost in new[]{false,true})
        foreach(int prize in present?new[]{0}:new[]{0,1,2,3,4,5})
        foreach(int free in new[]{0,1})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);save.WriteWramByte(0xc600,0x34);save.WriteWramByte(0xc601,0x12);
            InitializeTransientSession(save);var data=new WildTokayGameDatabase();int room=present?0xe5:0xde,level=Math.Min(prize,4);
            _inventory.GiveTreasure(TreasureId.Bracelet,1);_inventory.GiveTreasure(TreasureId.Bombs,1);_inventory.AddRupees(20);_inventory.SetScriptedEquippedItems(0,0);
            _saveData.SetGlobalFlag(data.FinishedGameFlag);_saveData.SetGlobalFlag(data.BeganSecretFlag);_saveData.WriteWramByte(data.WildLevelAddress,(byte)level);_saveData.SetRoomFlag(2,room,0x40);
            LoadValidationRoom(2,room);var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==(present?0x19:0x0d));F1Retain(slot);
            _player.WarpTo(new(80,72));_runtimeState.SetWramByte(0xcfde,lost?(byte)0xff:(byte)1);_runtimeState.SetWramByte(0xcfdd,(byte)prize);
            var game=_roomEvents.Get<WildTokayGameEvent>();var host=SomariaPrivate<WildTokayManagerScriptHost>(game,"_manager");
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,72);native[0xcfde]=lost?(byte)0xff:(byte)1;native[0xcfdd]=(byte)prize;
            int at=0xd040+_entities.InteractionSlot(slot.Npc)*256;native[at]=1;native[at+1]=0x48;native[at+2]=(byte)slot.Npc.Record.SubId;native[at+11]=(byte)slot.Npc.Position.Y;native[at+13]=(byte)slot.Npc.Position.X;
            while(14-InteractionAdmissionPool.Count>free)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);_entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int p=0xd040+_entities.InteractionSlot(puff)*256;native[p]=1;native[p+1]=0x48;native[p+2]=0x1b;native[p+4]=1;
            }
            string source=$"Wild result/present{present}/lost{lost}/prize{prize}/free{free}/batch{batch}";int updates=0,held=0;
            for(int i=0;i<190;i++)
            {
                if(_dialogue.IsOpen&&++held>=3)
                {
                    if(_dialogue.ChoiceActive){_dialogue.SubmitChoiceForValidation(1);native[0xcba5]=1;}else _dialogue.Close();native[0xcba0]=0;held=0;
                }
                else if(!_dialogue.IsOpen)held=0;
                StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:()=>
                {
                    native.AdvanceInteractions(_entities.FrameCounter);updates++;
                    FailIf(game.Counter!=native[at+6]||_runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02]||
                        _saveData.GetRoomFlags(2,room)!=native[0xc700+room]||_saveData.ReadWramByte(data.WildLevelAddress)!=native[data.WildLevelAddress]||
                        _saveData.ReadWramByte(0xc6b0)!=native[0xc6b0]||_saveData.ReadWramByte(0xc6b1)!=native[0xc6b1],
                        source+$" update{updates}: reward counter, raw masks, room flag, level or bomb capacity differs (cmd{host.CurrentCommandIndex}, counter{game.Counter}/{native[at+6]}, flags{_saveData.GetRoomFlags(2,room):x2}/{native[0xc700+room]:x2}, level{_saveData.ReadWramByte(data.WildLevelAddress)}/{native[data.WildLevelAddress]}).");
                    var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95],source+": ring RNG draw differs.");
                    foreach(int address in Enumerable.Range(0xc69a,16).Concat(new[]{0xc6ad,0xc6ae,0xc6be}))
                        FailIf(_saveData.ReadWramByte(address)!=native[address],source+$" update{updates}: prize inventory byte${address:x4} differs.");
                    foreach(int address in Enumerable.Range(0xc5c0,64).Append(0xc6cd))
                        FailIf(_saveData.ReadWramByte(address)!=native[address],source+$" update{updates}: unappraised ring byte${address:x4} differs.");
                    if(_dialogue.IsOpen)
                    {
                        int id=native.Word(0xcba2)-0x400;
                        if(id>=0x0a00&&id<=0x0a6c)
                        {
                            string expected=new TokayInteractionDatabase().Text(id);
                            if(id is 0x0a50 or 0x0a53)expected=expected.Replace("\\secret1",new LinkedGameNpcDatabase().GenerateSecret(0x15,_saveData),StringComparison.Ordinal);
                            FailIf(_dialogue.CurrentMessage!=DialogueBox.PlainText(expected),source+$" update{updates}: source reward text TX_${id:x4} differs.");
                        }
                    }
                    native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
                });
                if(i>10&&!_dialogue.IsOpen&&_runtimeState.ReadWramByte(0xcc8a)==0)break;
            }
            FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||_saveData.HasGlobalFlag(data.DoneSecretFlag)!=(present&&!lost),source+": ending branch did not release input or publish the present quest flag.");
            game.Cancel();_dialogue.Close();_entities.Clear();
        }
    }
}
