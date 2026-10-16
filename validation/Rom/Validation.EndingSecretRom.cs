using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEndingSecretRom()
    {
        // Bounded native states, not a credits playthrough: save, decline,
        // decline/retry/save, cancellation, and the two full-pool signal gates.
        for(int fixture=0;fixture<6;fixture++)
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            _saveData.WriteWramByte(0xc600,0x34);_saveData.WriteWramByte(0xc601,0x12);
            _saveData.WriteWramByte(0xc629,4);_dialogue.MessageSpeed=4;
            _saveData.WriteWramByte(WramAddress.wMapleKillCounter,17);
            _saveData.WriteWramByte(WramAddress.wMakuMapTextPresent,1);_saveData.WriteWramByte(WramAddress.wMakuMapTextPast,2);
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();var type=typeof(BlackTowerEndingEvent);
            type.GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            type.GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            var screen=_scene.EndingScreen;var scene=_scene;int saves=_saveWriteRequests;
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0);
            rom.InitializeLinkGameplay();rom.EnableVramDma();rom.EnableResetHandoff();var menu=rom.CreateMenuView();
            menu.LoadHudGraphics();menu.LoadRoomGraphics();rom.LoadCutsceneGraphics(0x98,0xa9);
            for(int address=0xd000;address<0xe000;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,3),(0xcbc2,4),(0xcbc3,255),(0xcbc9,0),(0xcfde,fixture==5?0:19),(WramAddress.wTilesetAnimation,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,pressed=0,secretAt=0,firstHoldAt=0,secondHoldAt=0,dialogAt=0,promptCount=0,continuedAt=0,continuedHoldAt=0;
            bool checkedSecret=false,checkedContinued=false,poolFull=false;int capturedSaves=saves;var sounds=_sound.AttachPlayRequestAudit();
            void Picture(string label)
            {
                using var image=screen.BackgroundTexture!.GetImage();
                for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                {
                    int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                    int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8,bank=(flags>>3)&1;
                    int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                    var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                    FailIf(rgb!=menu.HudColor(false,flags&7,shade),$"{label} pixel{x},{y}: port${rgb:x4}/ROM${menu.HudColor(false,flags&7,shade):x4}; tile${tile:x2}/flags${flags:x2}/shade{shade}/alpha{color.A}; port map${SomariaPrivate<byte[]>(screen,"_map")[map-0x9800]:x2}/attr${SomariaPrivate<byte[]>(screen,"_attributes")[map-0x9800]:x2}.");
                }
            }
            void Compare()
            {
                for(int address=0xc622;address<0xc626;address++)if(++rom[address]!=0)break;
                rom[0xc482]=(byte)pressed;rom[0xc481]=(byte)pressed;
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();update++;
                if(menu.ResetHandoff)
                {
                    FailIf(!scene.IsQueuedForDeletion()||owner.HasState||_frontendIntro?.Stage!=FrontendIntroStage.Boot,
                        "Ending reset must leave the object pass and enter the application boot owner.");return;
                }
                bool hadText=rom[0xcba0]!=0;rom.UpdateCredits(update);
                // The text thread can follow TX aliases and change wTextIndex;
                // compare the original showtext ID before that thread runs.
                if(!hadText&&rom[0xcba0]!=0)
                    FailIf(_dialogue.SourceTextId!=(rom[0xcba2]|((rom[0xcba3]-4)<<8)),"Completion showtext must retain its original TX_550d/TX_550e ID.");
                menu.AdvanceText();
                FailIf(owner.State!=rom[0xcbc2]||_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||
                    _dialogue.IsOpen!=(rom[0xcba0]!=0),$"Secret fixture{fixture} update{update}: state{owner.State}/{rom[0xcbc2]}, text{_dialogue.IsOpen}/{rom[0xcba0]} (native text${menu.TextState:x2}).");
                if(_dialogue.IsOpen)
                    FailIf(_dialogue.Position.Y!=96||rom[0xcbac]!=3,"Completion text must retain native position $03/SCY=$00.");
                foreach(int address in new[]{0xcbb3,0xcfde})FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Secret update{update}: byte${address:x4} differs.");
                foreach(int address in new[]{0xc600,0xc601,0xc612,0xc613,0xc614,0xc641,0xc6e6,0xc6e7,0xc62b,0xc62c,0xc62d,0xc62e,0xc62f,0xc630,0xc631})
                    FailIf(_saveData.ReadWramByte(address)!=rom[address],$"Completion update{update}: save byte${address:x4} port${_saveData.ReadWramByte(address):x2}/ROM${rom[address]:x2}.");
                FailIf(_saveData.HasGlobalFlag(0x14)!=((rom[0xc6d0+0x14/8]&(1<<(0x14&7)))!=0),"Completion global flag $14 differs.");
                if(_saveWriteRequests!=capturedSaves)
                {
                    capturedSaves=_saveWriteRequests;byte[] payload=_saveData.Serialize();
                    for(int index=0;index<payload.Length;index++)foreach(bool backup in new[]{false,true})
                        FailIf(payload[index]!=menu.SavedByte(0,0xc5b0+index,backup),$"Completion save {(backup?"backup":"primary")} byte${0xc5b0+index:x4} differs.");
                }
                if(owner.State==5)
                {
                    if(secretAt==0)secretAt=update;
                    if(!_transitions.NativePaletteFadeActive&&firstHoldAt==0)firstHoldAt=update;
                    if(!checkedSecret)
                    {
                        checkedSecret=true;
                        byte[] text=new LinkedGameNpcDatabase().GenerateGameTransferSecretText(_saveData);
                        FailIf(!text.SequenceEqual(Enumerable.Range(0,24).Select(index=>rom.BankByte(7,0xd460+index)))||text[5]!=0x20||text[11]!=0||text[17]!=0x20||text[23]!=0,
                            "Transfer text must preserve the native 5/5/5/5 groups and four gap bytes.");
                        byte[] font=OracleTileRenderer.CopyTextCharacterGraphics(text,0xff);
                        for(int index=0;index<font.Length;index++)FailIf(font[index]!=rom.VramByte(1,0x8c00+index),$"Secret font byte${index:x3} differs from native copyTextCharacterGfx/UNCMP $2b.");
                        Picture("Transfer secret");
                    }
                }
                if(owner.State==6&&secondHoldAt==0)secondHoldAt=update;
                if(owner.State==7&&dialogAt==0)
                {
                    dialogAt=update;
                    FailIf(secondHoldAt-firstHoldAt!=59||dialogAt-secondHoldAt!=60||
                        !poolFull&&(!_entities.EntityAdapters<GameCompleteDialogRoomEntity>().Any()||_saveData.RespawnRoom!=0x38||_saveData.RespawnX!=0x50||_saveData.RespawnY!=0x68),
                        "The two $3c holds and same-update $d1 initialization/preset $01 must retain their native boundaries.");
                }
                if(owner.State==9&&continuedAt==0)continuedAt=update;
                if(owner.State==10&&continuedHoldAt==0)continuedHoldAt=update;
                if(owner.State>=9&&!checkedContinued)
                {checkedContinued=true;Picture("TO BE CONTINUED");}
                if(owner.State>=9&&update>continuedAt)
                {
                    FailIf(screen.Parts.Count!=10||screen.OamOffset!=new Vector2I(0x38,0x30),"TO BE CONTINUED must retain its ten native OAM cells and $30,$38 origin.");
                    for(int index=0;index<screen.Parts.Count;index++)
                    {
                        var part=screen.Parts[index];int at=0xcb00+index*4;
                        FailIf(((part.Y+0x30)&255)!=rom[at]||((part.X+0x38)&255)!=rom[at+1]||part.Tile!=rom[at+2]||part.Flags!=rom[at+3],"TO BE CONTINUED OAM differs.");
                    }
                }
                FailIf(_random.CaptureState().Calls!=seed.Calls||rom.RandomCalls!=0||!sounds.Requests.SequenceEqual(rom.Sounds),$"Ending secret update{update}: RNG or sound requests differ.");
                pressed=0;
            }
            void Step(int count=1,int keys=0,bool batched=false)
            {pressed=keys;StepGameplayUpdates(count,Vector2.Zero,MenuRomActions(keys),MenuRomActions(keys),batched,Compare);}
            while(update<1800&&owner.State<10)
            {
                if(fixture>=4&&owner.State==6&&_runtimeState.ReadWramByte(0xcbb3)<=4)
                {
                    poolFull=true;
                    for(int index=0;index<14;index++)
                    {
                        var filler=_entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                        int at=0xd040+_entities.InteractionSlot(filler)*256;rom[at]=1;rom[at+1]=5;rom[at+2]=0x80;rom[at+11]=144;rom[at+13]=224;
                    }
                    Step(_runtimeState.ReadWramByte(0xcbb3)+2);
                    FailIf(owner.State!=(fixture==4?8:7)||_runtimeState.ReadWramByte(0xcfde)!=(fixture==4?19:0)||
                        _entities.EntityAdapters<GameCompleteDialogRoomEntity>().Any()||_saveWriteRequests!=saves,"A failed $d1 allocation must advance to state7 and preserve the previous $cfde signal.");
                    owner.Cancel();_entities.Clear();break;
                }
                if(fixture==3&&_dialogue.IsOpen)break;
                int choicePhase=DialoguePrivate<int>(_dialogue,"_choicePhase");
                if(_dialogue.IsOpen&&choicePhase==2)
                {
                    int target=fixture==0?1:fixture==1?(promptCount==0?0:1):promptCount<2?0:1;
                    if(_dialogue.SelectedChoice!=target){Step(keys:0x10);continue;}
                    promptCount++;Step(keys:1);continue;
                }
                // Page scrolling and choice delays use ordinary physical A,
                // even though Link's simulated input remains $ff/keys=$00.
                if(_dialogue.IsOpen)Step(keys:update%3==0?1:0);
                else Step(owner.State is 5 or 6?4:1,batched:fixture==1);
            }
            FailIf(update>=1800||!checkedSecret,"Completion prompt exceeded its bounded native fixture.");
            if(fixture>=4)continue;
            if(fixture==3)
            {
                owner.Cancel();StepGameplayUpdates(2,Vector2.Zero);
                FailIf(_dialogue.IsOpen||screen.Visible||_saveWriteRequests!=saves||_saveData.IsCompleted||_entities.EntityAdapters<GameCompleteDialogRoomEntity>().Any(),"Cancelling $d1 must release its text/presentation/slot without saving.");
                continue;
            }
            FailIf(!checkedContinued||promptCount!=(fixture==2?3:fixture==1?2:1)||_saveWriteRequests-saves!=(fixture==1?0:1)||_saveData.IsCompleted!=(fixture!=1),"Completion save/decline/retry branch differs.");
            while(_runtimeState.ReadWramByte(0xcbb3)!=0)Step(Math.Min(4,(int)_runtimeState.ReadWramByte(0xcbb3)),keys:1,batched:fixture==1);
            FailIf(update-continuedHoldAt!=180||owner.State!=10,"TO BE CONTINUED must ignore A through all 180 decrement updates.");
            Step(keys:0x0e);FailIf(owner.State!=10,"Only A can leave TO BE CONTINUED.");Step(keys:1);
            while(update<2200&&!menu.ResetHandoff)Step();
            FailIf(!menu.ResetHandoff||_saveWriteRequests-saves!=(fixture==1?0:1),"Ending reset must occur once after the white fade without another save.");
        }
    }
}
