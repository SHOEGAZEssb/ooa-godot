using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCreditsConstructionRom()
    {
        // Complete individual/batched updates, plus cancellation during impact.
        foreach(int fixture in new[]{0,1,2})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0);
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            for(int address=0xd000;address<0xe000;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,3),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,maxDebris=0;bool sawMove=false,sawSprout=false,sawDrop=false,sawShake=false,sawReload=false;
            var checkedStatueFrames=new HashSet<string>();
            var sounds=_sound.AttachPlayRequestAudit();
            void Actor(NpcCharacter actor,int at)
            {
                FailIf(actor.CurrentAnimationCounter!=rom[at+0x20]||actor.CurrentAnimationParameter!=rom[at+0x21]||
                    actor.Visible!=((rom[at+0x1a]&128)!=0)||actor.Position.Floor()!=new Vector2(rom[at+13],rom[at+11])||
                    actor.Visible&&actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                    $"Construction actor${rom[at+1]:x2}:${rom[at+2]:x2} update{update}: port{actor.Position}/anim{actor.CurrentAnimationCounter},{actor.CurrentAnimationParameter}/visible{actor.Visible}/{actor.ZIndex}; ROM{rom[at+13]},{rom[at+11]}/anim{rom[at+0x20]},{rom[at+0x21]}/visible${rom[at+0x1a]:x2}.");
            }
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=rom[0xcc2d]||_currentRoom.Id!=rom[0xcc30]||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.Visible||_hud.Visible,
                    $"Construction update{update}: room{_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}/{rom[0xcc2d]:x}:{rom[0xcc30]:x2}, state{owner.State}/{rom[0xcbc2]}.");
                foreach(int address in new[]{0xcfc0,0xcfde,0xcfdf,0xc486,WramAddress.wTilesetAnimation})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Construction byte${address:x4} update{update}: port${_runtimeState.ReadWramByte(address):x2}/ROM${rom[address]:x2}.");
                FailIf(SomariaPrivate<int>(_roomView,"_nativeScrollY")!=rom[0xc486],"Native SCY must scroll the background while actors keep world coordinates.");
                var ambi=_entities.EntityAdapters<BlackTowerEndingActor>().Single();Actor(ambi.Actor,0xd240);
                FailIf(ambi.State!=rom[0xd244]||ambi.Substate!=rom[0xd245],"Construction Ambi must retain the native dispatch state.");
                foreach(var worker in _entities.EntityAdapters<PickaxeWorkerCreditsRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(worker.Node)*256;Actor(worker.Actor,at);
                    FailIf(worker.State!=rom[at+4]||worker.Substate!=rom[at+5]||worker.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        worker.SubId==1&&worker.Substate==2&&worker.Counter!=rom[at+6],
                        $"Construction worker${worker.SubId:x2} update{update}: port{worker.State}:{worker.Substate}/{worker.PrecisePosition}/{worker.Counter}, ROM{rom[at+4]}:{rom[at+5]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{rom[at+6]}.");
                }
                var statue=_entities.EntityAdapters<MakuStatueConstructionRoomEntity>().Single();Actor(statue.Actor,0xd540);
                FailIf(SomariaPrivate<bool>(statue.Actor,"_sourceGrayscaleInverted"),"spr_linkstatue.properties requires invert:false for both unfinished and finished statue frames.");
                if(!_transitions.NativePaletteFadeActive&&checkedStatueFrames.Add(statue.Actor.CurrentScriptAnimationSource))
                    CompareInteractionSpritePixelsRom(rom,statue.Actor,0xd540,9,"Construction credits Link statue $6b:$10");
                FailIf(statue.State!=rom[0xd544]||statue.Substate!=rom[0xd545]||statue.PrecisePosition!=new Vector2(rom.Word(0xd54c)/256f,rom.Word(0xd54a)/256f)||
                    statue.ZFixed!=unchecked((short)rom.Word(0xd54e))||statue.SpeedZ!=unchecked((short)rom.Word(0xd554))||
                    statue.Substate is 1 or 6&&statue.Counter!=rom[0xd546],
                    $"Construction statue update{update}: port{statue.State}:{statue.Substate}/{statue.PrecisePosition}/Z{statue.ZFixed},{statue.SpeedZ}/{statue.Counter}; ROM{rom[0xd544]}:{rom[0xd545]}/{rom.Word(0xd54c)/256f},{rom.Word(0xd54a)/256f}/Z{unchecked((short)rom.Word(0xd54e))},{unchecked((short)rom.Word(0xd554))}/{rom[0xd546]}.");
                var sprout=_entities.EntityAdapters<MakuSproutRoomEntity>().Single();Actor(sprout.Npc,0xd640);
                FailIf(SomariaPrivate<int>(sprout,"_creditsState")!=rom[0xd644]||SomariaPrivate<int>(sprout,"_creditsSubstate")!=rom[0xd645],"Credits Maku Sprout must remain hidden until signal $06, then animate.");
                foreach(var debris in _entities.EntityAdapters<ExplosionWithDebrisRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(debris.Node)*256;Actor(debris.Actor,at);
                    FailIf(rom[at+1]!=0x99||debris.SubId!=2||debris.State!=rom[at+4]||debris.Angle!=rom[at+9]||debris.Speed!=rom[at+16]||
                        debris.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||debris.ZFixed!=unchecked((short)rom.Word(at+14))||debris.SpeedZ!=unchecked((short)rom.Word(at+20)),
                        $"Construction debris update{update} slot${at:x4} differs from native $99:$02.");
                }
                int count=_entities.EntityAdapters<ExplosionWithDebrisRoomEntity>().Count();maxDebris=System.Math.Max(maxDebris,count);
                FailIf(count!=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0x99),"Construction debris must preserve checked allocations, slot order and deletion.");
                sawMove|=statue.Substate==3;sawDrop|=statue.Substate==5;sawSprout|=sprout.Npc.Visible;sawShake|=rom[0xc486]==255;
                bool justReloaded=!sawReload&&_currentRoom.Id==0x38;sawReload|=justReloaded;
                if(update==1||justReloaded)
                {
                    using var image=_scene.EndingScreen.BackgroundTexture!.GetImage();
                    // Fixed tile samples cover both imported top-row overlays.
                    for(int y=0;y<144;y+=8)for(int x=0;x<160;x+=8)
                    {
                        int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&0x20)==0?0:7,sy=(flags&0x40)==0?0:7,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(rgb!=menu.HudColor(false,flags&7,shade),$"Construction background update{update} pixel{x},{y} differs after native room load.");
                    }
                }
                var rng=_random.CaptureState();FailIf(rng.Calls-seed.Calls!=rom.RandomCalls||rng.Rng1!=rom[0xff94]||rng.Rng2!=rom[0xff95]||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Construction RNG/sounds update{update}: port{rng.Calls-seed.Calls}/{string.Join(',',sounds.Requests)}, ROM{rom.RandomCalls}/{string.Join(',',rom.Sounds)}.");
            }
            while(update<2000&&owner.State!=2&&!(fixture==2&&sawShake))
                StepGameplayUpdates(update==0?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            FailIf(checkedStatueFrames.Count!=3,"Construction must compare the unfinished, carved and landed statue pixels with native OBJ graphics and palette $06.");
            FailIf(update>=2000||maxDebris==0||!sawMove||!sawReload||!sawSprout||!sawDrop||!sawShake,"Construction credits must move and finish the statue, reveal the sprout, and complete its impact shake.");
            if(fixture!=2)
            {
                while(owner.State==2)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>
                {rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);FailIf(owner.State!=rom[0xcbc2],"Construction caption must wait for white fade completion.");});
                FailIf(owner.State!=3||_runtimeState.ReadWramByte(0xcbb3)!=90||rom[0xcbb3]!=90||!_scene.EndingScreen.Visible||_runtimeState.ReadWramByte(0xc486)!=0,
                    "The fourth caption must begin with its 90-update hold and restored SCY.");
            }
            owner.Cancel();StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<PickaxeWorkerCreditsRoomEntity>().Any()||_entities.EntityAdapters<MakuStatueConstructionRoomEntity>().Any()||
                _entities.EntityAdapters<MakuSproutRoomEntity>().Any()||_entities.EntityAdapters<ExplosionWithDebrisRoomEntity>().Any()||
                _runtimeState.ReadWramByte(0xc486)!=0||!_hud.Visible,"Construction cancellation must retire the scene and reset background scroll.");
        }
    }
}
