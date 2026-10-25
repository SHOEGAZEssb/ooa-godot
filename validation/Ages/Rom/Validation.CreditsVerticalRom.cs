using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCreditsVerticalRom()
    {
        // One isolated roll and its batched replay; one short full-pool/cancel fixture.
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
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,2),(0xcbc2,0),(0xcfdf,0),(AgesWramAddress.wTilesetAnimation,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,lastLine=-1,signalAt=0,holdAt=0;bool sawBank1=false,sawAlias=false,poolInjected=false;
            var checkedCells=new HashSet<(int,int)>();var fillers=new List<Node2D>();
            var screen=_scene.EndingScreen;
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||
                    _player.Visible||_hud.Visible||!screen.Visible,$"Staff roll update{update}: port{owner.Stage}:{owner.State}/ROM{rom[0xcbc1]}:{rom[0xcbc2]}.");
                foreach(int address in new[]{0xcbb3,0xcbb4,0xcfde,0xcfdf})FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Staff roll byte${address:x4} update{update} differs.");
                var actors=_entities.EntityAdapters<CreditsVerticalRoomEntity>().ToArray();
                FailIf(actors.Length!=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0xaf),"Staff roll must retain source allocations/deletions.");
                foreach(var actor in actors)
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(actor.Child!=(rom[at+2]!=0)||actor.State!=rom[at+4]||actor.Line!=rom[at+(actor.Child?3:6)]||
                        actor.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||!actor.Child&&actor.Wait!=rom.Word(at+0x30),
                        $"Staff roll $af slot${at:x4} update{update}: port{actor.Child}/{actor.State}/line{actor.Line}/{actor.Position}/wait{actor.Wait}; ROM{rom[at+2]}/{rom[at+4]}/line{rom[at+(actor.Child?3:6)]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/wait{rom.Word(at+0x30)}.");
                    if(actor.Child)lastLine=System.Math.Max(lastLine,actor.Line);
                }
                if(update==1)
                {
                    FailIf(SomariaPrivate<int>(screen,"_scrollY")!=0x70,"GFX register state $09 must set SCY=$70 for the staff roll.");
                    var bytes=SomariaPrivate<byte[]>(screen,"_creditsSpriteBytes");var defined=SomariaPrivate<bool[]>(screen,"_creditsSpriteDefined");
                    for(int offset=0;offset<bytes.Length;offset++)FailIf(defined[offset]&&bytes[offset]!=rom.VramByte(offset>>12,0x8000+(offset&0xfff)),$"Staff roll VRAM bank{offset>>12}:${0x8000+(offset&0xfff):x4} differs from GFXH $95.");
                    using var image=screen.BackgroundTexture!.GetImage();
                    for(int y=0;y<256;y+=8)for(int x=0;x<160;x+=8)
                    {
                        int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&0x20)==0?0:7,sy=(flags&0x40)==0?0:7,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(rgb!=menu.HudColor(false,flags&7,shade),$"Staff roll background pixel{x},{y} differs from GFXH $95.");
                    }
                }
                FailIf(screen.ObjectOam.Count!=rom[0xff9f]/4,$"Staff roll OAM count update{update}: port{screen.ObjectOam.Count}/ROM{rom[0xff9f]/4}.");
                for(int index=0;index<screen.ObjectOam.Count;index++)
                {
                    var part=screen.ObjectOam[index];int at=0xcb00+index*4;
                    FailIf(part.Y!=rom[at]||part.X!=rom[at+1]||part.Tile!=rom[at+2]||part.Flags!=rom[at+3],$"Staff roll OAM update{update} cell{index}: port{part}/ROM{rom[at]},{rom[at+1]},{rom[at+2]},{rom[at+3]}.");
                    sawBank1|=(part.Flags&8)!=0;sawAlias|=(part.Flags&8)!=0&&part.Tile>=0x80;
                    if(!checkedCells.Add((part.Tile,part.Flags)))continue;
                    using var image=SomariaPrivate<Dictionary<(int,int),Texture2D>>(screen,"_objectCells")[(part.Tile,part.Flags)].GetImage();
                    for(int y=0;y<16;y++)for(int x=0;x<8;x++)
                    {
                        int sx=(part.Flags&0x20)==0?x:7-x,sy=(part.Flags&0x40)==0?y:15-y,bank=(part.Flags>>3)&1,address=0x8000+(part.Tile&~1)*16+sy*2;
                        int shade=((rom.VramByte(bank,address)>>(7-sx))&1)|(((rom.VramByte(bank,address+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(shade==0?color.A!=0:color.A!=1||rgb!=menu.HudColor(true,part.Flags&7,shade),$"Staff roll glyph ${part.Tile:x2}, flags${part.Flags:x2}, pixel{x},{y} differs from resident VRAM.");
                    }
                }
                if(signalAt==0&&rom[0xcfdf]!=0)signalAt=update;
                if(holdAt==0&&owner.State==2)holdAt=update;
                FailIf(_random.CaptureState().Calls!=seed.Calls||rom.RandomCalls!=0,"The normal staff roll must not consume RNG.");
            }
            while(update<6500&&owner.Stage==2&&!(fixture==2&&poolInjected&&screen.ObjectOam.Count!=0))
            {
                if(fixture==2&&!poolInjected&&_entities.EntityAdapters<CreditsVerticalRoomEntity>().Any(actor=>!actor.Child&&actor.Wait<=3)&&!_transitions.NativePaletteFadeActive)
                {
                    poolInjected=true;
                    for(int index=0;index<13;index++)
                    {
                        var filler=_entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));fillers.Add(filler);
                        int at=0xd040+_entities.InteractionSlot(filler)*256;rom[at]=1;rom[at+1]=5;rom[at+2]=0x80;rom[at+11]=144;rom[at+13]=224;
                    }
                    StepGameplayUpdates(3,Vector2.Zero,afterUpdate:Compare);
                    FailIf(_entities.EntityAdapters<CreditsVerticalRoomEntity>().Single().Line!=1,"A failed $af child allocation must still advance the line and load its next wait.");
                    foreach(var filler in fillers)
                    {
                        int at=0xd040+_entities.InteractionSlot(filler)*256;for(int offset=0;offset<64;offset++)rom[at+offset]=0;
                        _entities.RetireEntity(_entities.EntityAdapters<IRoomEntity>().Single(entity=>entity.Node==filler));
                    }
                    fillers.Clear();
                }
                StepGameplayUpdates(owner.State==2&&_runtimeState.ReadWramByte(0xcbb4)==0&&_runtimeState.ReadWramByte(0xcbb3)<=4?1:4,
                    Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            }
            FailIf(update>=6500||fixture!=2&&(owner.Stage!=3||lastLine!=16||!sawBank1||!sawAlias||holdAt!=signalAt+1||update-holdAt!=480),"Staff roll must show all seventeen lines and retain its exact 480-update terminal hold.");
            if(fixture==2)
            {
                var child=_entities.EntityAdapters<CreditsVerticalRoomEntity>().Single(actor=>actor.Child);var position=child.Position;
                _transitions.BeginNativeWhiteFade(true);rom.BeginCutsceneWhiteFade();
                StepGameplayUpdates(3,Vector2.Zero,afterUpdate:Compare);
                FailIf(child.Position!=position||screen.ObjectOam.Count!=0,"A palette fade must pause $af movement and clear direct text OAM before cancellation.");
            }
            owner.Cancel();StepGameplayUpdates(2,Vector2.Zero);
            FailIf(screen.Visible||screen.ObjectOam.Count!=0||_entities.EntityAdapters<CreditsVerticalRoomEntity>().Any()||!_hud.Visible,"Cancelling staff roll must release direct OAM and all $af slots.");
        }
    }
}
