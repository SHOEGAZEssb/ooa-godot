using Godot;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCreditsHorizontalRom()
    {
        foreach(int mode in Enumerable.Range(0,3))foreach(int scene in Enumerable.Range(0,mode==0?8:1))foreach(bool batched in new[]{false,true})
        {
            int card=mode==1?1:scene;
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(card>=4);LoadValidationRoom(1,0x65);_entities.Clear();
            var palettesBefore=_world.BackgroundPalettes.Capture();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            rom[0xcc01]=(byte)(card>=4?1:0); // checkIsLinkedGame reads the live copy, not file byte $c612.
            for(int address=0xd000;address<0xe000;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,2),(0xcfde,card),(0xcfdf,255),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            var screen=_scene.EndingScreen;var sounds=_sound.AttachPlayRequestAudit();int update=0,firstParent=0,firstChild=0,maskUploads=0;
            Image? previousFont=null;bool sawMask=false,sawRestored=false,backgroundChecked=false;
            var checkedCells=new HashSet<(int,int)>();
            bool poolInjected=false;var fillers=new List<Node2D>();
            void Compare()
            {
                rom[0xff9f]=0;rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);rom.DrawCutsceneObjects();
                FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||
                    _player.Visible||_hud.Visible||!screen.Visible,
                    $"Credits card${card:x2} update{update}: port{owner.Stage}:{owner.State}, ROM{rom[0xcbc1]}:{rom[0xcbc2]}.");
                foreach(int address in new[]{0xcbb3,0xcfde,0xcfdf})FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Credits card${card:x2} byte${address:x4} update{update}.");
                var actors=_entities.EntityAdapters<CreditsHorizontalRoomEntity>().ToArray();
                int nativeCount=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0xae);
                FailIf(actors.Length!=nativeCount,$"Credits $ae allocations card${card:x2} update{update}: port{actors.Length}/ROM{nativeCount}.");
                foreach(var actor in actors)
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(rom[at+1]!=0xae||actor.SubId!=rom[at+2]||actor.Child!=(rom[at+3]!=0)||actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||
                        actor.Counter!=rom[at+6]||actor.NextAnimation!=rom[at+7]||actor.Position!=new Vector2(rom[at+13],rom[at+11])||
                        (actor.Child?actor.Travel!=rom[at+0x30]:actor.Wait!=rom.Word(at+0x30)||actor.MaskState!=rom[at+0x33]),
                        $"Credits $ae card${card:x2} update{update} slot${at:x4}: port {actor.Child}/{actor.State}:{actor.Substate}/{actor.Counter}/{actor.NextAnimation}/{actor.Position}/wait{actor.Wait}/travel{actor.Travel}/mask{actor.MaskState}, ROM {rom[at+3]}/{rom[at+4]}:{rom[at+5]}/{rom[at+6]}/{rom[at+7]}/{rom[at+13]},{rom[at+11]}/wait{rom.Word(at+0x30)}/mask{rom[at+0x33]}.");
                    if(!actor.Child){if(firstParent==0)firstParent=update;sawMask|=actor.Substate==1;sawRestored|=actor.MaskState==3;}
                    else if(firstChild==0)firstChild=update;
                }
                var font=SomariaPrivate<Image>(screen,"_ownedSprites");
                if(!ReferenceEquals(font,previousFont))
                {
                    if(previousFont is not null)maskUploads++;
                    previousFont=font;
                    checkedCells.Clear();
                    var bytes=SomariaPrivate<byte[]>(screen,"_creditsSpriteBytes");
                    var defined=SomariaPrivate<bool[]>(screen,"_creditsSpriteDefined");
                    for(int offset=0;offset<0x800;offset++)FailIf(defined[offset]&&bytes[offset]!=rom.VramByte(0,0x8000+offset),
                        $"Credits font card${card:x2} update{update} VRAM${0x8000+offset:x4}: port${bytes[offset]:x2}/ROM${rom.VramByte(0,0x8000+offset):x2}.");
                }
                // The source compacts wrapped/cullable OBJ parts into $cb00,
                // with a shared forty-cell capacity, in native slot order.
                FailIf(screen.ObjectOam.Count!=rom[0xff9f]/4,$"Credits OAM count card${card:x2} update{update}: port{screen.ObjectOam.Count}/ROM{rom[0xff9f]/4}.");
                for(int index=0;index<screen.ObjectOam.Count;index++)
                {
                    var part=screen.ObjectOam[index];int at=0xcb00+index*4;
                    FailIf(part.Y!=rom[at]||part.X!=rom[at+1]||part.Tile!=rom[at+2]||part.Flags!=rom[at+3],
                        $"Credits OAM card${card:x2} update{update} cell{index}: port{part}/ROM{rom[at]},{rom[at+1]},{rom[at+2]},{rom[at+3]}.");
                    if(!checkedCells.Add((part.Tile,part.Flags)))continue;
                    var cells=SomariaPrivate<Dictionary<(int,int),Texture2D>>(screen,"_objectCells");using var image=cells[(part.Tile,part.Flags)].GetImage();
                    for(int y=0;y<16;y++)for(int x=0;x<8;x++)
                    {
                        int sx=(part.Flags&0x20)==0?x:7-x,sy=(part.Flags&0x40)==0?y:15-y,address=0x8000+(part.Tile&~1)*16+sy*2;
                        int shade=((rom.VramByte(0,address)>>(7-sx))&1)|(((rom.VramByte(0,address+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(shade==0?color.A!=0:color.A!=1||rgb!=menu.HudColor(true,part.Flags&7,shade),
                            $"Credits glyph card${card:x2} tile${part.Tile:x2} pixel{x},{y} differs from native OBJ palette/2bpp data.");
                    }
                }
                if(!backgroundChecked)
                {
                    backgroundChecked=true;using var image=screen.BackgroundTexture!.GetImage();
                    FailIf(SomariaPrivate<int>(screen,"_scrollX")!=rom[0xc487],"Credits SCX must retain the native alternating $00/$d0 value.");
                    for(int y=0;y<144;y++)for(int x=0;x<256;x++)
                    {
                        int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Credits background card${card:x2} pixel{x},{y} differs from native VRAM/PALH $a1.");
                    }
                }
                var rng=_random.CaptureState();FailIf(rng.Calls-seed.Calls!=rom.RandomCalls||rng.Rng1!=rom[0xff94]||rng.Rng2!=rom[0xff95]||!sounds.Requests.SequenceEqual(rom.Sounds),"Horizontal credits changed shared sound/RNG order.");
            }
            while(update<1800&&(rom[0xcfdf]!=255||update==0)&&!(mode==1&&sawRestored))
            {
                if(mode==2&&!poolInjected&&_entities.EntityAdapters<CreditsHorizontalRoomEntity>().Any(actor=>!actor.Child&&actor.State==1&&actor.Substate==0&&actor.Wait<=4))
                {
                    poolInjected=true;
                    for(int index=0;index<13;index++)
                    {
                        var filler=_entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));fillers.Add(filler);
                        int at=0xd040+_entities.InteractionSlot(filler)*256;rom[at]=1;rom[at+1]=5;rom[at+2]=0x80;rom[at+11]=144;rom[at+13]=224;
                    }
                }
                StepGameplayUpdates(update==0||_entities.EntityAdapters<CreditsHorizontalRoomEntity>().Any(actor=>!actor.Child&&actor.Substate==2&&actor.Counter<=4)?1:4,
                    Vector2.Zero,batched:batched,afterUpdate:Compare);
                if(fillers.Count!=0)
                {
                    FailIf(firstChild!=0||_entities.EntityAdapters<CreditsHorizontalRoomEntity>().Single().NextAnimation!=1,"A failed $ae child allocation must still advance the parent's animation and script.");
                    foreach(var filler in fillers)
                    {
                        int at=0xd040+_entities.InteractionSlot(filler)*256;
                        // Match a completed interaction's deletion, rather
                        // than leaving the puff's live state in a disabled slot.
                        for(int offset=0;offset<64;offset++)rom[at+offset]=0;
                        _entities.RetireEntity(_entities.EntityAdapters<IRoomEntity>().Single(entity=>entity.Node==filler));
                    }
                    fillers.Clear();
                }
            }
            FailIf(update>=1800||firstParent==0||firstChild-firstParent!=(mode==2?80:32)||mode==2&&!poolInjected||
                (card&3)!=0&&(!sawMask||!sawRestored||mode!=1&&maskUploads!=31),
                $"Credits card${card:x2} must spawn after 32 fade-free updates, preserve all 31 mask uploads and publish $cfdf: update{update}, parent{firstParent}, child{firstChild}, uploads{maskUploads}.");
            // Follow the native completion once: it chooses the next vignette
            // or staff roll, and begins the fade before any replacement load.
            if(mode!=1)
            {
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
                int last=card>=4?7:3;
                FailIf(!_transitions.NativePaletteFadeActive||card<last&&(_runtimeState.ReadWramByte(0xcfde)!=card+1||owner.State!=0)||card==last&&owner.Stage!=2,
                    "Horizontal credits must hand off through the source stage/index boundary.");
            }
            owner.Cancel();
            FailIf(screen.Visible||screen.ObjectOam.Count!=0||_entities.EntityAdapters<CreditsHorizontalRoomEntity>().Any()||!_hud.Visible||
                !palettesBefore.Cast<Color>().SequenceEqual(_world.BackgroundPalettes.Capture().Cast<Color>()),"Cancelling credits must release all $ae slots, OAM and shared presentation.");
        }
    }
}
