using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEndingFinaleRom()
    {
        string[] keys=["attack","item","inventory",""];
        for(int fixture=0;fixture<keys.Length;fixture++)
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            _inventory.SetScriptedHealth(4);_inventory.SetScriptedEquippedItems(5,0);
            typeof(InventoryState).GetProperty(nameof(InventoryState.ActiveRing))!.SetValue(_inventory,0x60);
            _saveData.WriteWramByte(WramAddress.wActiveRing,0x60);
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();var type=typeof(BlackTowerEndingEvent);
            type.GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            type.GetField("_healthBeforeCredits",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,8);
            type.GetField("_ringBeforeCredits",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,0x20);
            type.GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            type.GetMethod("Profile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,["credits-scroll",0xa0]);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0);
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();rom.LoadCutsceneGraphics(0x95,0xa0);
            for(int address=0xd000;address<0xe000;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            rom[0xcbe4]=(byte)_statusBar.DisplayedHealth;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,2),(0xcbc2,2),(0xcbb3,1),(0xcbb4,0),(0xcde3,1),(0xcde4,5),(0xcbc3,255),(0xcbc9,0),(WramAddress.wTilesetAnimation,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,holdAt=0,waitingAt=0,pressed=0;bool pictureChecked=false;var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom[0xc482]=(byte)pressed;rom[0xc481]=(byte)pressed;
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||
                    _player.Visible||_hud.Visible||!_scene.EndingScreen.Visible,$"Finale update{update}: port{owner.Stage}:{owner.State}/ROM{rom[0xcbc1]}:{rom[0xcbc2]}.");
                FailIf(_statusBar.DisplayedHealth!=rom[0xcbe4],"The native credits caller omits status-bar updates while restoring live health.");
                foreach(int address in new[]{0xcbb3,0xcbb4,WramAddress.wLinkHealth,WramAddress.wInventoryB,WramAddress.wInventoryA,WramAddress.wActiveRing})
                {
                    int actual=address is 0xcbb3 or 0xcbb4?_runtimeState.ReadWramByte(address):_saveData.ReadWramByte(address);
                    FailIf(actual!=rom[address],$"Finale byte${address:x4} update{update}: port${actual:x2}/ROM${rom[address]:x2}.");
                }
                if(owner.Stage==3&&owner.State==2&&holdAt==0)holdAt=update;
                if(owner.Stage==3&&owner.State==3&&waitingAt==0)waitingAt=update;
                if(owner.Stage==3&&owner.State==1&&!pictureChecked)
                {
                    pictureChecked=true;FailIf(_inventory.HealthQuarters!=_inventory.MaxHealthQuarters||_inventory.ActiveRing!=0x20||_inventory.EquippedB!=1||_inventory.EquippedA!=5,
                        "func_60f1 must restore full health, saved equips and active ring before THE END.");
                    using var image=_scene.EndingScreen.BackgroundTexture!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(rgb!=menu.HudColor(false,flags&7,shade),$"THE END pixel{x},{y} differs from native GFXH $98/PALH $a9.");
                    }
                }
                FailIf(_random.CaptureState().Calls!=seed.Calls||rom.RandomCalls!=0||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"THE END update{update}: RNG port{_random.CaptureState().Calls-seed.Calls}/ROM{rom.RandomCalls}, sounds port{string.Join(',',sounds.Requests)}/ROM{string.Join(',',rom.Sounds)}.");
            }
            if(fixture==3)
            {
                StepGameplayUpdates(6,Vector2.Zero,afterUpdate:Compare);
                FailIf(owner.State!=0||_inventory.HealthQuarters!=4,"The incoming staff-roll fade must finish before restoring ending inventory.");
            }
            else
            {
                // Early A edges are ignored during the incoming/outgoing fades and hold.
                pressed=1;StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:Compare);pressed=0;
                bool heldKeyChecked=false;
                while(update<700&&owner.State!=3)
                {
                    if(!heldKeyChecked&&owner.State==2&&_runtimeState.ReadWramByte(0xcbb3) is >10 and <200)
                    {
                        pressed=1;StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:Compare);pressed=0;heldKeyChecked=true;
                        FailIf(owner.State!=2,"A during the 256-update hold must be ignored.");
                    }
                    StepGameplayUpdates(owner.State==2&&_runtimeState.ReadWramByte(0xcbb3)<=4?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
                }
                FailIf(update>=700||!pictureChecked||waitingAt-holdAt!=256,"THE END must retain the source's overwritten zero byte and exact 256-update hold.");
                pressed=4;StepGameplayUpdates(1,Vector2.Zero,["map"],["map"],afterUpdate:Compare);pressed=0;
                FailIf(owner.State!=3,"Select must not leave THE END.");
                pressed=fixture==0?1:fixture==1?2:8;
                StepGameplayUpdates(1,Vector2.Zero,[keys[fixture]],[keys[fixture]],batched:fixture==1,afterUpdate:Compare);
                FailIf(owner.State!=4||!_transitions.NativePaletteFadeActive,"A, B and Start must begin the source white fade toward the transfer secret.");
            }
            owner.Cancel();StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_inventory.HealthQuarters!=(fixture==3?8:_inventory.MaxHealthQuarters)||_inventory.EquippedB!=1||_inventory.EquippedA!=5||
                _inventory.ActiveRing!=0x20||!_hud.Visible||_scene.EndingScreen.Visible,"Ending cancellation must release presentation without undoing completed full-health restoration.");
        }
    }
    private void ValidateGameTransferSecretRom()
    {
        var codec=new LinkedGameNpcDatabase();
        for(int fixture=0;fixture<4;fixture++)
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName(fixture==0?"Link":"AbCdE");save.NameChild(fixture==0?"Pip":"ZyXwV");
            foreach(var pair in new[]{(0xc600,fixture==2?0:0x34+fixture),(0xc601,fixture==2?0:0x12),(0xc60f,fixture*21),(0xc610,fixture+0x0b),
                (0xc611,1),(0xc612,fixture==3?1:0),(0xc613,fixture==1?1:0),(0xc615,fixture&1),(0xc622,0x56),(0xc623,0x23)})save.WriteWramByte(pair.Item1,(byte)pair.Item2);
            var rom=new FrontendRom();for(int address=0xc600;address<=0xc6ff;address++)rom[address]=save.ReadWramByte(address);
            rom.Call(0x481b,3); // bank3.s:generateGameTransferSecret, including its shared encoder/cipher/text conversion.
            var values=codec.GenerateGameTransferSecretValues(save);
            FailIf(values.Length!=20||!values.SequenceEqual(Enumerable.Range(0,20).Select(index=>rom.BankByte(7,0xd478+index))),$"Transfer secret fixture{fixture}: six-bit output differs from the executed clean-US encoder.");
            foreach(int address in new[]{0xc600,0xc601,0xc612,0xc613,0xc6fd,0xc6fe})FailIf(save.ReadWramByte(address)!=rom[address],$"Transfer secret fixture{fixture}: byte${address:x4} differs after generation.");
            FailIf(rom.RandomCalls!=0||!values.SequenceEqual(codec.GenerateGameTransferSecretValues(save)),"Transfer generation must preserve RNG and repeat the source secret after restoring file flags.");
        }
    }
}
