using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedCreditsTroupeRom()
    {
        foreach(int fixture in new[]{0,1,2})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetGlobalFlag(AgesGlobalFlag.MakuTreeDisappeared);LoadValidationRoom(0,0x38);_entities.Clear();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,null);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,PostObjectCollisionsEnabled=false,NativeGameKeysEnabled=true};
            rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            for(int at=0xd000;at<0xe000;at++)rom[at]=0;
            rom[0xcc01]=1;rom[0xcc03]=1;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,5)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0;bool sawJump=false,sawScript=false,checkedBackground=false;var frames=new HashSet<(int,int)>();var sounds=_sound.AttachPlayRequestAudit();
            while(update<1200&&owner.State!=2&&!(fixture==2&&sawJump))
                StepGameplayUpdates(update==0?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(update>=1200||!sawJump||!checkedBackground||fixture!=2&&(!sawScript||owner.State!=2),"Linked troupe must complete eight source animal movements, fixed performers and the word-counter finish signal.");
            owner.Cancel();
            FailIf(_entities.EntityAdapters<HoronDogCreditsRoomEntity>().Any()||_entities.EntityAdapters<TroupeMemberRoomEntity>().Any()||
                _entities.EntityAdapters<DinRoomEntity>().Any()||!_hud.Visible,"Troupe cancellation must retire its actual actor slots and restore HUD ownership.");
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=rom[0xcc2d]||_currentRoom.Id!=rom[0xcc30]||
                    _transitions.NativePaletteThreadMode!=rom[0xc4ab]||_runtimeState.ReadWramByte(0xcfdf)!=rom[0xcfdf]||
                    _runtimeState.ReadWramByte(AgesWramAddress.wTilesetAnimation)!=rom[AgesWramAddress.wTilesetAnimation],$"Linked troupe update{update}: controller state differs.");
                Actor(_entities.EntityAdapters<DinRoomEntity>().Single().Actor);
                foreach(var member in _entities.EntityAdapters<TroupeMemberRoomEntity>())Actor(member.Actor);
                foreach(var dog in _entities.EntityAdapters<HoronDogCreditsRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(dog)*256;Actor(dog.Actor);
                    FailIf(dog.State!=rom[at+4]||dog.Substate!=rom[at+5]||dog.Counter!=rom[at+6]||dog.Counter2!=rom[at+7]||dog.Angle!=rom[at+9]||dog.Speed!=rom[at+16]||
                        dog.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||dog.ZFixed!=unchecked((short)rom.Word(at+14))||
                        dog.SpeedZ!=unchecked((short)rom.Word(at+20))||dog.Landed!=rom[at+0x37],
                        $"Troupe animal${dog.Subid:x2} update{update}: state{dog.State}:{dog.Substate}/{rom[at+4]}:{rom[at+5]},count{dog.Counter}/{dog.Counter2}/{rom[at+6]}/{rom[at+7]},angle{dog.Angle}/{rom[at+9]},speed{dog.Speed}/{rom[at+16]},Z{dog.ZFixed}/{unchecked((short)rom.Word(at+14))},speedZ{dog.SpeedZ}/{unchecked((short)rom.Word(at+20))}.");
                    sawJump|=dog.Substate==1&&dog.ZFixed!=0;sawScript|=dog.Subid==7&&dog.Substate==2&&dog.Speed==0x14;
                    FailIf(dog.Actor.ScriptDrawOffset.Y!=(rom[at+15]>=128?rom[at+15]-256:rom[at+15]),"Troupe Z presentation must retain source signed high byte.");
                }
                if(!checkedBackground&&!_transitions.NativePaletteFadeActive)
                {
                    checkedBackground=true;using var image=_scene.EndingScreen.BackgroundTexture!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),at=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&32)==0?x%8:7-x%8,sy=(flags&64)==0?y%8:7-y%8,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,at+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,at+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Linked troupe GFXH $8f pixel({x},{y}) differs.");
                    }
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),$"Linked troupe update{update}: RNG/audio differs.");
            }
            void Actor(NpcCharacter actor)
            {
                int at=0xd040+_entities.InteractionSlot(actor)*256;
                CompareInteractionOamRom(rom,actor,at,$"Troupe update{update}");
                FailIf(actor.CurrentAnimationCounter!=rom[at+0x20]||actor.CurrentAnimationParameter!=rom[at+0x21]||
                    actor.Position!=new Vector2(rom[at+13],rom[at+11])||actor.Visible!=((rom[at+0x1a]&128)!=0)||actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                    $"Troupe actor${actor.Record.Id:x2}:${actor.Record.SubId:x2} update{update}: animation/position/visibility/priority differs.");
                if(actor.Visible&&!_transitions.NativePaletteFadeActive&&frames.Add((at,rom.Word(at+0x1e))))
                {
                    var bytes=ValidationRom.LoadCleanUs().Span;int count=bytes[0x14*0x4000+(rom.Word(at+0x1e)&0x3fff)];
                    if(count!=0)CompareInteractionSpritePixelsRom(rom,actor,at,count,$"Troupe actor${actor.Record.Id:x2}:${actor.Record.SubId:x2}");
                }
            }
        }
    }
}
