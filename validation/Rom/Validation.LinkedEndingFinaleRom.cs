using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedEndingFinaleRom()
    {
        for(int fixture=0;fixture<4;fixture++)
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetGlobalFlag(GlobalFlag.MakuTreeDisappeared);
            _saveData.WriteWramByte(WramAddress.wFileIsHeroGame,(byte)(fixture==2?1:0));LoadValidationRoom(0,0x38);_entities.Clear();
            _inventory.SetScriptedHealth(4);_inventory.SetScriptedEquippedItems(5,0);_inventory.SetScriptedActiveRing(0x60);
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();var type=typeof(BlackTowerEndingEvent);
            type.GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            type.GetField("_healthBeforeCredits",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,8);
            type.GetField("_ringBeforeCredits",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,0x20);
            type.GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            type.GetMethod("Profile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,["credits-scroll",0xa0]);
            var screen=_scene.EndingScreen;new EndingScreenDatabase().ApplyPalette(0x9f,_world.BackgroundPalettes,_entities);
            screen.Load("credits-waving",retainGraphics:true);screen.SetScrollY(0x98);_transitions.SetCutsceneCameraOrigin(new(0x10,0x98));
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,PostObjectCollisionsEnabled=false,NativeGameKeysEnabled=true};
            rom.InitializeLinkGameplay();rom.EnableVramDma();rom.EnableResetHandoff();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            rom.LoadCutsceneGraphics(0x95,0xa0);rom.LoadCutsceneGraphics(0x9a,0x9f);
            for(int at=0xd000;at<0xe000;at++)rom[at]=0;
            rom[0xcc01]=1;rom[0xcc03]=1;rom[0xcd17]=0xd0;rom[0xffac]=0x10;rom[0xffaa]=0x98;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,3),(0xcbc2,0),(0xcde3,1),(0xcde4,5),(0xc486,0x98),(0xcfde,0),(WramAddress.wTilesetAnimation,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,pressed=0,holdAt=0,waitAt=0,firstAt=0,secondAt=0,secretWait=0,saves=_saveWriteRequests;
            bool checkedEnd=false,checkedSecret=false;var scene=_scene;var sounds=_sound.AttachPlayRequestAudit();var checkedCells=new HashSet<(int,int)>();
            while(update<1200&&!menu.ResetHandoff&&!(fixture==3&&update>40)&&!(fixture==2&&waitAt!=0&&update>waitAt+40))
            {
                string[] edges=[];pressed=0;
                if(owner.State==3&&waitAt!=0){pressed=fixture==1?2:1;edges=[fixture==1?"item":"attack"];}
                if(owner.State==7&&update==secretWait+1){pressed=2;edges=["item"];}
                if(owner.State==7&&update==secretWait+2){pressed=8;edges=["inventory"];}
                if(owner.State==7&&secretWait!=0&&update>secretWait+3){pressed=1;edges=["attack"];}
                int count=fixture==1&&pressed==0&&owner.State is 1 or 2 or 5 or 6?4:1;
                StepGameplayUpdates(count,Vector2.Zero,edges,edges,batched:fixture==1,afterUpdate:Compare);
            }
            _sound.SetRequestObserver(null);
            FailIf(update>=1200||!checkedEnd||fixture<2&&(!checkedSecret||!menu.ResetHandoff||waitAt-holdAt!=256||secretWait-secondAt!=180||secondAt-firstAt!=59),"Linked finale must retain its hold, waves, Hero's Secret and A-only reset handoff.");
            FailIf(_saveWriteRequests!=saves,"The linked Hero's Secret path must never spawn the normal completion save dialog or save to disk.");
            if(fixture>=2){owner.Cancel();FailIf(_entities.EntityAdapters<CreditsShipRoomEntity>().Any()||screen.Visible||!_hud.Visible,"Finale cancellation must release ship actors and screen ownership.");}
            void Compare()
            {
                for(int address=0xc622;address<0xc626;address++)if(++rom[address]!=0)break;
                rom[0xff9f]=0;rom[0xc482]=(byte)pressed;rom[0xc481]=(byte)pressed;rom[0xcc00]=(byte)_entities.FrameCounter;
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();update++;
                if(menu.ResetHandoff){FailIf(!scene.IsQueuedForDeletion()||owner.HasState||_frontendIntro?.Stage!=FrontendIntroStage.Boot,"Linked reset must return through the application boot owner.");return;}
                rom.UpdateCredits(_entities.FrameCounter);rom.DrawCutsceneObjects();
                FailIf(owner.State!=rom[0xcbc2]||_transitions.NativePaletteThreadMode!=rom[0xc4ab]||_dialogue.IsOpen||_entities.EntityAdapters<GameCompleteDialogRoomEntity>().Any(),$"Linked finale update{update}: controller/palette/dialog differs.");
                foreach(int address in new[]{0xcbb3,0xcbb4,0xcbba,0xc486,0xcfde})FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Linked finale update{update} byte${address:x4}: port${_runtimeState.ReadWramByte(address):x2}/ROM${rom[address]:x2}.");
                foreach(int address in new[]{WramAddress.wLinkHealth,WramAddress.wInventoryA,WramAddress.wInventoryB,WramAddress.wActiveRing,0xc612,0xc613})
                    FailIf(_saveData.ReadWramByte(address)!=rom[address],$"Linked finale update{update}: inventory/file byte${address:x4} differs.");
                if(owner.State==2&&holdAt==0)holdAt=update;if(owner.State==3&&waitAt==0)waitAt=update;
                if(owner.State==5&&!_transitions.NativePaletteFadeActive&&firstAt==0)firstAt=update;
                if(owner.State==6&&secondAt==0)secondAt=update;if(owner.State==7&&secretWait==0)secretWait=update;
                if(owner.State==1&&!checkedEnd){checkedEnd=true;Picture("Linked THE END");}
                if(owner.State==5&&!checkedSecret)
                {
                    checkedSecret=true;Picture("Hero's Secret");
                    var text=new LinkedGameNpcDatabase().GenerateGameTransferSecretText(_saveData);
                    FailIf(!text.SequenceEqual(Enumerable.Range(0,24).Select(index=>rom.BankByte(7,0xd460+index))),"Hero's Secret text must match the native flag transform and secret encoder.");
                }
                foreach(var actor in _entities.EntityAdapters<CreditsShipRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(actor)*256;
                    FailIf(actor.State!=rom[at+4]||actor.Subid!=rom[at+2]||actor.Counter!=rom[at+6]||actor.Z!=unchecked((sbyte)rom[at+15])||actor.Position!=new Vector2(rom[at+13],rom[at+11]),$"Ship slot${at:x4} update{update}: native phase/bob differs.");
                }
                if(owner.State<4)
                {
                    FailIf(screen.ObjectOam.Count!=rom[0xff9f]/4,$"Ship update{update}: OAM count differs.");
                    for(int index=0;index<screen.ObjectOam.Count;index++)
                    {
                        var part=screen.ObjectOam[index];int at=0xcb00+index*4;
                        FailIf(part.Y!=rom[at]||part.X!=rom[at+1]||part.Flags!=rom[at+3],$"Ship update{update} OAM{index}: position/flags differs.");
                        if(_transitions.NativePaletteFadeActive||!checkedCells.Add((part.Tile,part.Flags)))continue;
                        using var image=SomariaPrivate<Dictionary<(int,int),Texture2D>>(screen,"_objectCells")[(part.Tile,part.Flags)].GetImage();
                        for(int y=0;y<16;y++)for(int x=0;x<8;x++)
                        {
                            int sx=(part.Flags&32)==0?x:7-x,sy=(part.Flags&64)==0?y:15-y,address=0x8000+(rom[at+2]&~1)*16+sy*2;
                            int shade=((rom.VramByte(0,address)>>(7-sx))&1)|(((rom.VramByte(0,address+1)>>(7-sx))&1)<<1);
                            var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                            FailIf(shade==0?color.A!=0:color.A!=1||rgb!=menu.HudColor(true,part.Flags&7,shade),$"Ship update{update},tile${part.Tile:x2}/ROM${rom[at+2]:x2},flags${part.Flags:x2},pixel{x},{y}: port${rgb:x4}/alpha{color.A},ROMshade{shade}/color${menu.HudColor(true,part.Flags&7,shade):x4}.");
                        }
                    }
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),$"Linked finale update{update}: wave RNG/audio differs.");
            }
            void Picture(string label)
            {
                using var image=screen.BackgroundTexture!.GetImage();
                for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                {
                    int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                    int sx=(flags&32)==0?x%8:7-x%8,sy=(flags&64)==0?y%8:7-y%8,bank=(flags>>3)&1;
                    int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                    var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                    FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"{label} pixel{x},{y}: port${rgb:x4}/ROM${menu.HudColor(false,flags&7,shade):x4}.");
                }
            }
        }
    }
}
