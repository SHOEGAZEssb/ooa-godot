using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedCreditsCompanionsRom()
    {
        foreach(int fixture in new[]{0,1,2,3})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetGlobalFlag(AgesGlobalFlag.MakuTreeDisappeared);LoadValidationRoom(0,0x38);_entities.Clear();
            _inventory.SetScriptedHealth(4);_inventory.SetScriptedActiveRing(0x60);
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();var type=typeof(BlackTowerEndingEvent);
            type.GetField("_initialize",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,false);
            type.GetField("_healthBeforeCredits",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,8);
            type.GetField("_ringBeforeCredits",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,0x20);
            type.GetMethod("BeginScreen",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,null);
            type.GetField("_restoreTerrain",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,_currentRoom.ClearNativeCutsceneTerrain());
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,PostObjectCollisionsEnabled=false,NativeGameKeysEnabled=true};
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            for(int at=0xd000;at<0xe000;at++)rom[at]=0;
            rom[0xcc01]=1;rom[0xcc03]=1;rom[0xcd17]=0xd0;
            _runtimeState.SetWramByte(0xcd17,0xd0);
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,6),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0;bool sawDimitri=false,sawRicky=false,sawJump=false,checkedBackground=false;var frames=new HashSet<(int,int)>();var sounds=_sound.AttachPlayRequestAudit();
            while(update<2600&&owner.State!=2&&!(fixture==2&&sawDimitri)&&!(fixture==3&&sawJump))
                StepGameplayUpdates(update<2?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(update>=2600||!checkedBackground||!sawDimitri||fixture!=2&&(!sawRicky||!sawJump)||fixture<2&&owner.State!=2,"Linked companions must preserve Moosh/Dimitri/Ricky replacement, normal Link input, and the final jump/hide/exit signal.");
            if(fixture<2)
            {
                while(owner.State==2)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>
                {rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);FailIf(owner.State!=rom[0xcbc2],"Companion caption must wait for the complete native fade.");});
                FailIf(owner.State!=3||_runtimeState.ReadWramByte(0xcbb3)!=90||rom[0xcbb3]!=90||_player.Visible||
                    SomariaPrivate<int>(_player,"_topDownAirZFixed")!=0||_entities.EntityAdapters<CompanionCreditsRoomEntity>().Any()||
                    _entities.EntityAdapters<CreditsBananaRoomEntity>().Any(),"Caption $06 must clear the companion slot, banana and frozen Link air before its90-update hold.");
            }
            owner.Cancel();
            FailIf(_entities.EntityAdapters<CompanionCreditsRoomEntity>().Any()||_entities.EntityAdapters<CreditsBananaRoomEntity>().Any()||
                _inventory.ActiveRing!=0x20||_inventory.HealthQuarters!=8||!_hud.Visible||_runtimeState.ReadWramByte(0xcbc3)!=0||CompanionRuntimeState.AnyActive(_runtimeState),
                "Cancelling companion credits must release the slot, child, simulated input and pre-credits inventory.");
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||_transitions.NativePaletteThreadMode!=rom[0xc4ab]||_runtimeState.ReadWramByte(0xcfdf)!=rom[0xcfdf],$"Companion credits update{update}: controller differs.");
                foreach(int at in new[]{0xcd17,0xcc2c,0xcbc3,0xcbc4,0xcbc5,AgesWramAddress.wDisabledObjects})
                    FailIf(_runtimeState.ReadWramByte(at)!=rom[at],$"Companion credits byte${at:x4} update{update}: port${_runtimeState.ReadWramByte(at):x2}/ROM${rom[at]:x2}.");
                var companion=_entities.EntityAdapters<CompanionCreditsRoomEntity>().Single();
                FailIf(CompanionRuntimeState.AnyActive(_runtimeState)!=(rom[0xd100]!=0),"Credits companions must occupy the authoritative shared special-object slot.");
                FailIf(companion.Id!=rom[0xd101]||companion.State!=rom[0xd104]||companion.Substate!=rom[0xd105]||companion.Counter!=rom[0xd106]||companion.Counter2!=rom[0xd107]||
                    companion.Direction!=rom[0xd108]||companion.Angle!=rom[0xd109]||companion.Speed!=rom[0xd110]||companion.ZFixed!=unchecked((short)rom.Word(0xd10e))||
                    companion.SpeedZ!=unchecked((short)rom.Word(0xd114))||companion.PrecisePosition!=new Vector2(rom.Word(0xd10c)/256f,rom.Word(0xd10a)/256f)||
                    companion.Visible!=((rom[0xd11a]&128)!=0)||companion.AnimationCounter!=rom[0xd120],
                    $"Companion update{update}: ID${companion.Id:x2}/${rom[0xd101]:x2},state{companion.State}:{companion.Substate}/{rom[0xd104]}:{rom[0xd105]},count{companion.Counter}/{rom[0xd106]},angle{companion.Angle}/{rom[0xd109]},Z{companion.ZFixed}/{unchecked((short)rom.Word(0xd10e))},pos{companion.PrecisePosition}/{rom.Word(0xd10c)/256f},{rom.Word(0xd10a)/256f},anim{companion.AnimationIndex}:{companion.AnimationCounter}/{rom[0xd120]}.");
                sawDimitri|=companion.Id==0x10;sawRicky|=companion.Id==0x0f;
                if(companion.State!=0&&!_transitions.NativePaletteFadeActive&&frames.Add((companion.Id,rom.Word(0xd11e))))
                {
                    int pointer=rom.Word(0xd11e),bank=0x13+(pointer>>14),count=ValidationRom.LoadCleanUs().Span[bank*0x4000+(pointer&0x3fff)];
                    CompareInteractionSpritePixelsRom(rom,companion.Texture,companion.FrameOffset,0xd100,count,"Credits companion",bank);
                }
                foreach(var banana in _entities.EntityAdapters<CreditsBananaRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(banana)*256;CompareInteractionOamRom(rom,banana.Actor,at,"Credits banana");
                    FailIf(banana.Node.GetIndex()>=companion.Node.GetIndex(),"Native companion OAM precedes interaction OAM, so equal-priority banana cells must paint behind it.");
                    FailIf(rom[at+1]!=0xc0||banana.State!=rom[at+4]||banana.Direction!=rom[at+8]||banana.Actor.Position!=new Vector2(rom[at+13],rom[at+11])||
                        banana.Actor.CurrentAnimationCounter!=rom[at+0x20]||banana.Actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a])||
                        banana.Actor.ScriptDrawOffset.Y!=unchecked((sbyte)rom[at+15]),$"Banana update{update} differs from native related-object dispatch.");
                    if(!_transitions.NativePaletteFadeActive&&frames.Add((at,rom.Word(at+0x1e))))
                    {
                        int count=ValidationRom.LoadCleanUs().Span[0x14*0x4000+(rom.Word(at+0x1e)&0x3fff)];
                        CompareInteractionSpritePixelsRom(rom,banana.Actor,at,count,"Credits banana");
                    }
                }
                FailIf(_entities.EntityAdapters<CreditsBananaRoomEntity>().Count()!=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0xc0),"Banana must delete on Moosh's replacement in the same interaction pass.");
                if(!checkedBackground&&!_transitions.NativePaletteFadeActive)
                {
                    checkedBackground=true;using var image=_scene.EndingScreen.BackgroundTexture!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),at=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&32)==0?x%8:7-x%8,sy=(flags&64)==0?y%8:7-y%8,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,at+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,at+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Companion GFXH $91 pixel({x},{y}) differs.");
                    }
                }
                if(update>1)
                {
                    FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||_player.Visible!=((rom[0xd01a]&128)!=0)||
                        _inventory.ActiveRing!=rom[AgesWramAddress.wActiveRing]||_inventory.EquippedB!=rom[AgesWramAddress.wInventoryB]||_inventory.EquippedA!=rom[AgesWramAddress.wInventoryA]||
                        SomariaPrivate<int>(_player,"_topDownAirZFixed")!=unchecked((short)rom.Word(0xd00e)),
                        $"Companion Link update{update}: position{_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f},Z{SomariaPrivate<int>(_player,"_topDownAirZFixed")}/{unchecked((short)rom.Word(0xd00e))},ring{_inventory.ActiveRing}/{rom[AgesWramAddress.wActiveRing]}.");
                    sawJump|=rom[0xd00f]!=0;
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Companion update{update}: RNG/audio port{string.Join(',',sounds.Requests)}/ROM{string.Join(',',rom.Sounds)}.");
            }
        }
    }
}
