using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedCreditsStaffRollRom()
    {
        for(int fixture=0;fixture<4;fixture++)
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetGlobalFlag(GlobalFlag.MakuTreeDisappeared);LoadValidationRoom(0,0x38);_entities.Clear();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();var type=typeof(BlackTowerEndingEvent);
            type.GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            type.GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            type.GetMethod("Profile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,["credits-scroll",0xa0]);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,PostObjectCollisionsEnabled=false,NativeGameKeysEnabled=true};
            rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();rom.LoadCutsceneGraphics(0x95,0xa0);
            for(int at=0xd000;at<0xe000;at++)rom[at]=0;
            rom[0xcc01]=1;rom[0xcc03]=1;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,2),(0xcbc2,2),(0xcbb3,1),(0xcbb4,0),(0xcfdf,255),(0xc486,0x70),(WramAddress.wTilesetAnimation,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            _scene.EndingScreen.SetScrollY(0x70);_transitions.SetCutsceneCameraOrigin(new(0x10,0));rom[0xffac]=0x10;rom[0xffaa]=0;
            int update=0;bool sawGlow=false,sawWave=false,sawReplacement=false;var sounds=_sound.AttachPlayRequestAudit();
            var pictures=new HashSet<int>();var checkedCells=new HashSet<(int,int,int)>();
            while(update<2200&&owner.Stage==2&&!(fixture==2&&sawGlow)&&!(fixture==3&&sawWave))
                StepGameplayUpdates(owner.State==8&&_runtimeState.ReadWramByte(0xcbb3)<=4?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(update>=2200||!sawGlow||fixture<2&&(!sawWave||!sawReplacement||owner.Stage!=3),"Linked staff roll must finish the Triforce, waving actors and replacement map.");
            owner.Cancel();
            FailIf(_entities.EntityAdapters<CreditsSceneActorRoomEntity>().Any()||_scene.EndingScreen.ObjectOam.Count!=0||
                _transitions.WorldToGameplayScreen(Vector2.Zero)!=Vector2.Zero||!_hud.Visible,"Cancelling linked staff roll must release sprites, camera and presentation.");
            void Compare()
            {
                rom[0xcc00]=(byte)(_entities.FrameCounter);rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);rom.DrawCutsceneObjects();
                foreach(int address in new[]{0xcbc1,0xcbc2,0xcbb3,0xcbb4,0xcbba,0xc486,0xcfdf})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Linked staff roll update{update} byte${address:x4}: port${_runtimeState.ReadWramByte(address):x2}/ROM${rom[address]:x2}.");
                FailIf(_transitions.NativePaletteThreadMode!=rom[0xc4ab]||-_transitions.WorldToGameplayScreen(Vector2.Zero)!=new Vector2(rom[0xffac],rom[0xffaa]),$"Linked staff roll update{update}: palette/camera differs.");
                var actors=_entities.EntityAdapters<CreditsSceneActorRoomEntity>().ToArray();
                int nativeCount=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256] is 0x4a or 0xcf);
                FailIf(actors.Length!=nativeCount,$"Linked staff roll update{update}: actor count{actors.Length}/{nativeCount}.");
                foreach(var actor in actors)
                {
                    int at=0xd040+_entities.NativeInteractionSlot(actor)*256;
                    FailIf(actor.Id!=rom[at+1]||actor.Subid!=rom[at+2]||actor.Variant!=rom[at+3]||actor.State!=rom[at+4]||
                        actor.Position!=new Vector2(rom[at+13],rom[at+11])||actor.AnimationCounter!=rom[at+0x20]||actor.AnimationParameter!=rom[at+0x21],
                        $"Linked staff roll actor${actor.Id:x2}:${actor.Subid:x2} update{update}: position{actor.Position}/{rom[at+13]},{rom[at+11]},clock{actor.AnimationCounter}/{rom[at+0x20]},param{actor.AnimationParameter}/{rom[at+0x21]}.");
                }
                sawGlow|=actors.Count(actor=>actor.Id==0x4a&&actor.Subid==10)==9;sawWave|=actors.Count(actor=>actor.Id==0xcf)==3;
                sawReplacement|=owner.State==7&&rom[0xc486]>=0x60;
                var screen=_scene.EndingScreen;var offset=screen.OamOffset;
                int scene=owner.State<6?0:rom[0xc486]<0x60?1:2;
                if(!_transitions.NativePaletteFadeActive&&pictures.Add(scene))
                {
                    using var image=screen.BackgroundTexture!.GetImage();
                    for(int y=0;y<256;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&32)==0?x%8:7-x%8,sy=(flags&64)==0?y%8:7-y%8,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Staff roll scene{scene} pixel{x},{y} differs from native map/palette.");
                    }
                }
                var parts=screen.Parts.Select(part=>new IntroOamPart((part.Y+offset.Y)&255,(part.X+offset.X)&255,part.Tile,part.Flags))
                    .Where(part=>part.Y<160&&part.X<168).Concat(screen.ObjectOam).ToArray();
                FailIf(parts.Length!=rom[0xff9f]/4,$"Linked staff roll update{update}: OAM count{parts.Length}/{rom[0xff9f]/4}.");
                for(int index=0;index<parts.Length;index++)
                {
                    var part=parts[index];int at=0xcb00+index*4;
                    FailIf(part.Y!=rom[at]||part.X!=rom[at+1]||part.Tile!=rom[at+2]||part.Flags!=rom[at+3],$"Linked staff roll update{update} OAM{index}: {part}/ROM{rom[at]:x2},{rom[at+1]:x2},{rom[at+2]:x2},{rom[at+3]:x2}.");
                    if(_transitions.NativePaletteFadeActive||!checkedCells.Add((scene,part.Tile,part.Flags)))continue;
                    using var image=SomariaPrivate<Dictionary<(int,int),Texture2D>>(screen,"_objectCells")[(part.Tile,part.Flags)].GetImage();
                    for(int y=0;y<16;y++)for(int x=0;x<8;x++)
                    {
                        int sx=(part.Flags&32)==0?x:7-x,sy=(part.Flags&64)==0?y:15-y,bank=(part.Flags>>3)&1,address=0x8000+(part.Tile&~1)*16+sy*2;
                        int shade=((rom.VramByte(bank,address)>>(7-sx))&1)|(((rom.VramByte(bank,address+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(shade==0?color.A!=0:color.A!=1||rgb!=menu.HudColor(true,part.Flags&7,shade),$"Staff roll scene{scene} tile${part.Tile:x2}/flags${part.Flags:x2} pixel{x},{y} differs from native VRAM/palette.");
                    }
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),$"Linked staff roll update{update}: RNG/audio differs.");
            }
        }
    }
}
