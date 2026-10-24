using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedCreditsZeldaRom()
    {
        foreach(int fixture in new[]{0,1,2})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetGlobalFlag(GlobalFlag.MakuTreeDisappeared);LoadValidationRoom(0,0x38);_entities.Clear();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,null);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,PostObjectCollisionsEnabled=false,NativeGameKeysEnabled=true};
            rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            for(int at=0xd000;at<0xe000;at++)rom[at]=0;
            rom[0xcc01]=1;rom[0xcc03]=1;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,7)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,maxSparkles=0;bool sawFlight=false,sawStopped=false,checkedBackground=false;
            var frames=new HashSet<(int,int)>();var sounds=_sound.AttachPlayRequestAudit();
            while(update<1000&&owner.State!=2&&!(fixture==2&&maxSparkles>0))
                StepGameplayUpdates(update==0?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(update>=1000||!sawFlight||maxSparkles==0||!checkedBackground||fixture!=2&&(!sawStopped||owner.State!=2),
                "Linked vignette $07 must finish Zelda's walk/pause and the independently allocated flying sprite/sparkles.");
            owner.Cancel();
            FailIf(_entities.EntityAdapters<ZeldaEndingRoomEntity>().Any()||_entities.EntityAdapters<CreditsFlyingSpriteRoomEntity>().Any()||
                _entities.EntityAdapters<InteractionSparkleRoomEntity>().Any()||!_hud.Visible,"Cancelling linked Zelda credits must release its actors and sparkle slots.");
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=rom[0xcc2d]||_currentRoom.Id!=rom[0xcc30]||
                    _transitions.NativePaletteThreadMode!=rom[0xc4ab]||_runtimeState.ReadWramByte(0xcfdf)!=rom[0xcfdf]||
                    _runtimeState.ReadWramByte(WramAddress.wTilesetAnimation)!=rom[WramAddress.wTilesetAnimation],$"Linked Zelda vignette update{update}: controller/room/animation state differs.");
                var zelda=_entities.EntityAdapters<ZeldaEndingRoomEntity>().Single();Actor(zelda.Actor,zelda.PrecisePosition);
                sawStopped|=rom[0xd279]!=0;
                foreach(var flyer in _entities.EntityAdapters<CreditsFlyingSpriteRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(flyer)*256;Actor(flyer.Actor,flyer.PrecisePosition);
                    FailIf(flyer.State!=rom[at+4]||flyer.Substate!=rom[at+5]||flyer.Counter!=(flyer.Substate==0?rom.Word(at+6):rom[at+6])||
                        flyer.SparkleCounter!=rom[at+0x36],$"Linked flying sprite update{update}: source counters/substate differ.");
                    sawFlight|=flyer.Substate==1;
                }
                int nativeFlyers=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0xc1);
                FailIf(_entities.EntityAdapters<CreditsFlyingSpriteRoomEntity>().Count()!=nativeFlyers,"Linked flying sprite must delete on animation parameter $ff after movement.");
                foreach(var child in _entities.EntityAdapters<InteractionSparkleRoomEntity>())
                {
                    int at=0xd040+_entities.NativeInteractionSlot(child)*256;var sparkle=(InteractionSparkleEffect)child.Node;
                    FailIf(rom[at+1]!=0x84||rom[at+2]!=9||sparkle.Position!=new Vector2(rom[at+13],rom[at+11])||
                        sparkle.AnimationParameter!=rom[at+0x21]||sparkle.Visible!=((rom[at+0x1a]&128)!=0),$"Linked sparkle update{update}: $84:$09 allocation/position/animation differs.");
                }
                int count=_entities.EntityAdapters<InteractionSparkleRoomEntity>().Count();maxSparkles=Math.Max(maxSparkles,count);
                FailIf(count!=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0x84),"Linked sparkle count/lifetime differs.");
                if(!checkedBackground&&!_transitions.NativePaletteFadeActive)
                {
                    checkedBackground=true;using var image=_scene.EndingScreen.BackgroundTexture!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),at=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&32)==0?x%8:7-x%8,sy=(flags&64)==0?y%8:7-y%8,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,at+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,at+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Linked scene GFXH $93 pixel({x},{y}) differs.");
                    }
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),$"Linked Zelda update{update}: shared RNG/audio differs.");
            }
            void Actor(NpcCharacter actor,Vector2 position)
            {
                int at=0xd040+_entities.InteractionSlot(actor)*256;
                CompareInteractionOamRom(rom,actor,at,$"Linked vignette $07 update{update}");
                FailIf(actor.CurrentAnimationCounter!=rom[at+0x20]||actor.CurrentAnimationParameter!=rom[at+0x21]||
                    position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||actor.Visible!=((rom[at+0x1a]&128)!=0)||
                    actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),$"Linked actor${actor.Record.Id:x2} update{update}: animation/position/priority differs.");
                if(!_transitions.NativePaletteFadeActive&&frames.Add((actor.Record.Id,rom.Word(at+0x1e))))
                {
                    var bytes=ValidationRom.LoadCleanUs().Span;int count=bytes[0x14*0x4000+(rom.Word(at+0x1e)&0x3fff)];
                    if(count!=0)CompareInteractionSpritePixelsRom(rom,actor,at,count,$"Linked actor${actor.Record.Id:x2}");
                }
            }
        }
    }
}
