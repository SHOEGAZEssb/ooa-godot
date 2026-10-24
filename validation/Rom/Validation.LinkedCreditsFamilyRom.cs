using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedCreditsFamilyRom()
    {
        foreach(int fixture in new[]{0,1,2})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetGlobalFlag(GlobalFlag.MakuTreeDisappeared);LoadValidationRoom(0,0x38);_entities.Clear();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,null);
            if(fixture==1)for(int i=0;i<19;i++)_entities.NextRandomValue();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,PostObjectCollisionsEnabled=false,NativeGameKeysEnabled=true};
            rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            for(int at=0xd000;at<0xe000;at++)rom[at]=0;
            rom[0xcc01]=1;rom[0xcc03]=1;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,4)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0;bool sawPattern=false,sawTurn=false,checkedBackground=false;var frames=new HashSet<(int,int)>();var sounds=_sound.AttachPlayRequestAudit();
            while(update<1200&&owner.State!=2&&!(fixture==2&&sawPattern))
                StepGameplayUpdates(update==0?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(update>=1200||!sawPattern||!checkedBackground||fixture!=2&&(!sawTurn||owner.State!=2),"Linked family must preserve original child patterns, Nayru/Ralph turn RNG, fountains and scene completion.");
            owner.Cancel();
            FailIf(_entities.EntityAdapters<BoyPatternRoomEntity>().Any()||_entities.EntityAdapters<NayruRalphCreditsRoomEntity>().Any()||
                _entities.EntityAdapters<FountainDecorationRoomEntity>().Any()||!_hud.Visible,"Cancelling family credits must release its native actor slots and presentation.");
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=rom[0xcc2d]||_currentRoom.Id!=rom[0xcc30]||
                    _transitions.NativePaletteThreadMode!=rom[0xc4ab]||_runtimeState.ReadWramByte(0xcfdf)!=rom[0xcfdf]||_runtimeState.ReadWramByte(0xcfd0)!=rom[0xcfd0],
                    $"Linked family update{update}: native controller/signal differs.");
                foreach(var actor in _entities.EntityAdapters<NayruRalphCreditsRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(actor)*256;Actor(actor.Actor,actor.PrecisePosition);
                    FailIf(actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||actor.Counter!=(actor.Substate==3?rom.Word(at+6):rom[at+6])||
                        actor.TurnCounter!=rom[at+0x38]||actor.Direction!=rom[at+8]||actor.Speed!=rom[at+16],
                        $"DF${actor.Subid:x2} update{update}: state{actor.Substate}/{rom[at+5]},count{actor.Counter}/{rom.Word(at+6)},turn{actor.TurnCounter}/{rom[at+0x38]},direction{actor.Direction}/{rom[at+8]},speed{actor.Speed}/{rom[at+16]}.");
                    sawTurn|=actor.Direction!=0;
                }
                foreach(var actor in _entities.EntityAdapters<BoyPatternRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(actor)*256;Actor(actor.Actor,actor.PrecisePosition);
                    FailIf(actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||actor.Counter!=rom[at+6]||actor.Counter2!=rom[at+7]||
                        actor.PatternState!=rom[at+0x37]||actor.Angle!=rom[at+9]||actor.Speed!=rom[at+16]||
                        actor.ZFixed!=unchecked((short)rom.Word(at+14))||actor.SpeedZ!=unchecked((short)rom.Word(at+20)),
                        $"Boy${actor.Actor.Record.Id:x2}:${actor.Actor.Record.SubId:x2} update{update}: state{actor.Substate}/{rom[at+5]},counter{actor.Counter}/{rom[at+6]},pattern{actor.Counter2}:{actor.PatternState}/{rom[at+7]}:{rom[at+0x37]},angle{actor.Angle}/{rom[at+9]},Z{actor.ZFixed}/{unchecked((short)rom.Word(at+14))}.");
                    sawPattern|=actor.Substate==5;
                }
                FailIf(_entities.EntityAdapters<BoyPatternRoomEntity>().Count()!=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256] is 0x3c or 0x3f),"Family children must delete in their own source slots.");
                foreach(var fountain in _entities.EntityAdapters<FountainDecorationRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(fountain)*256;
                    FailIf(fountain.Position!=new Vector2(rom[at+13],rom[at+11])||fountain.Visible!=((rom[at+0x1a]&128)!=0)||fountain.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),"Family fountain source position/priority differs.");
                    if(!_transitions.NativePaletteFadeActive&&frames.Add((at,rom.Word(at+0x1e))))
                    {
                        int count=ValidationRom.LoadCleanUs().Span[0x14*0x4000+(rom.Word(at+0x1e)&0x3fff)];
                        CompareInteractionSpritePixelsRom(rom,fountain.Texture,fountain.FrameOffset,at,count,"Linked fountain");
                    }
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
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Family GFXH $8d pixel({x},{y}) differs.");
                    }
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),$"Family update{update}: native RNG/audio differs.");
            }
            void Actor(NpcCharacter actor,Vector2 precise)
            {
                CompareFullscreenActorPositionRom(rom,actor,0xd040+_entities.InteractionSlot(actor)*256,"Linked family credits");
                int at=0xd040+_entities.InteractionSlot(actor)*256;
                CompareInteractionOamRom(rom,actor,at,$"Family update{update}");
                FailIf(actor.CurrentAnimationCounter!=rom[at+0x20]||actor.CurrentAnimationParameter!=rom[at+0x21]||
                    precise!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||actor.Visible!=((rom[at+0x1a]&128)!=0)||actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                    $"Family actor${actor.Record.Id:x2}:${actor.Record.SubId:x2} update{update}: animation/position/priority differs.");
                if(!_transitions.NativePaletteFadeActive&&frames.Add((at,rom.Word(at+0x1e))))
                {
                    int count=ValidationRom.LoadCleanUs().Span[0x14*0x4000+(rom.Word(at+0x1e)&0x3fff)];
                    if(count!=0)CompareInteractionSpritePixelsRom(rom,actor,at,count,$"Family actor${actor.Record.Id:x2}:${actor.Record.SubId:x2}");
                }
            }
        }
    }
}
