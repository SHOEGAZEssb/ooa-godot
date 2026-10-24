using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedCreditsTitleRom()
    {
        foreach(bool batch in new[]{false,true})
        foreach(bool cancel in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetGlobalFlag(GlobalFlag.MakuTreeDisappeared);LoadValidationRoom(0,0x38);_entities.Clear();
            _inventory.SetScriptedEquippedItems(TreasureId.Shield,TreasureId.Sword);_inventory.SetActiveRingEnabled(true);
            int health=_inventory.HealthQuarters,ring=_inventory.ActiveRing;
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,null);
            typeof(BlackTowerEndingEvent).GetMethod("Profile",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,["reunion-makutree",0xc9]);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,PostObjectCollisionsEnabled=false,NativeGameKeysEnabled=true};
            rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();rom.LoadCutsceneGraphics(0x3c,0xc9);
            for(int at=0xd000;at<0xe000;at++)rom[at]=0;
            rom[0xcc01]=1;rom[0xcc03]=1;rom[0xcd17]=0xd0;
            // Independent $0a entry after reunion's separate $0f regression.
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,0),(0xcbc2,0),(0xcbb3,1),(0xcbb4,0)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,hold=0,fade=0;bool pixels=false;var sounds=_sound.AttachPlayRequestAudit();
            while(owner.Stage==0&&update<1100&&!(cancel&&fade!=0&&update-fade>=60))
                StepGameplayUpdates(_runtimeState.ReadWramByte(0xcbb3)<=4?1:4,Vector2.Zero,batched:batch,afterUpdate:()=>
                {
                    rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                    FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_transitions.NativePaletteThreadMode!=rom[0xc4ab]||
                        _inventory.HealthQuarters!=rom[0xc6aa]||_inventory.EquippedB!=rom[0xc688]||_inventory.EquippedA!=rom[0xc689]||_inventory.ActiveRing!=rom[0xc6cb],
                        $"Linked title update{update}: state, fade or shared inventory differs.");
                    foreach(int at in new[]{0xcbb3,0xcbb4,0xcde3,0xcde4,0xcfde,0xcfdf})
                        FailIf(_runtimeState.ReadWramByte(at)!=rom[at],$"Linked title update{update} byte${at:x4} differs.");
                    if(hold==0&&owner.State==1){hold=update;FailIf(rom.Word(0xcbb3)!=180,"Linked title must retain its 180-update hold.");}
                    if(fade==0&&owner.State==2){fade=update;FailIf(fade-hold!=180||rom.Word(0xcbb3)!=840,"Linked title must start its 840-update fade timer after 180 updates.");}
                    if(owner.Stage==1)FailIf(update-fade!=840||rom[0xcfde]!=4||rom[0xcfdf]!=0||owner.State!=0,"Linked title must select vignette $04 at the exact source boundary.");
                    if(!pixels&&owner.State==2&&!_transitions.NativePaletteFadeActive)
                    {
                        pixels=true;using var image=_scene.EndingScreen.BackgroundTexture!.GetImage();
                        for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                        {
                            int map=0x9c00+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),at=0x9000+unchecked((sbyte)tile)*16;
                            int sx=(flags&32)==0?x%8:7-x%8,sy=(flags&64)==0?y%8:7-y%8,bank=(flags>>3)&1;
                            int shade=((rom.VramByte(bank,at+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,at+sy*2+1)>>(7-sx))&1)<<1);
                            var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                            FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Linked title retained $9c00 map pixel({x},{y}) differs.");
                        }
                    }
                    FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),"Linked title RNG/audio differs.");
                });
            _sound.SetRequestObserver(null);
            FailIf(hold==0||fade==0||!cancel&&(!pixels||owner.Stage!=1),"Linked title must finish or cancel its native hold/fade.");
            owner.Cancel();
            FailIf(_inventory.HealthQuarters!=health||_inventory.ActiveRing!=ring||_inventory.EquippedB!=TreasureId.Shield||_inventory.EquippedA!=TreasureId.Sword||
                _scene.EndingScreen.Visible||!_hud.Visible,"Linked title cancellation must restore inventory and presentation ownership.");
        }
    }
}
