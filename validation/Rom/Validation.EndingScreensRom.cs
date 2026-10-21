using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerEndingScreensRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool cancelEarly in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x60,0x48));
            var backgroundBefore=_world.BackgroundPalettes.Capture();var layoutBefore=(byte[])_currentRoom.Layout.Clone();var collisionsBefore=(byte[])_currentRoom.Collisions.Clone();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            _runtimeState.SetWramByte(0xcbc2,0x11);
            // A real dynamic actor survives the palace fade until the native
            // state-$11 clear, rather than an already-empty pool fixture.
            _entities.AddEntity(new NayruRescueActorRoomEntity(SomariaPrivate<RoomEventContext>(_roomEvents,"_context"),
                SomariaPrivate<NayruRescueWarpDatabase>(_roomEvents.Get<NayruRescueWarpEvent>(),"_data"),0x36,4,new(0x40,0x48)));
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x60,0x48)
                {CompanionDispatchEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            rom[0xc2ef]=9;rom[0xcc03]=1;rom[0xcbc2]=0x11;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            rom[0xd240]=1;rom[0xd241]=0x36;rom[0xd242]=4;rom[0xd244]=1;rom[0xd24b]=0x48;rom[0xd24d]=0x40;
            rom[0xcbc3]=255;rom[0xcbc4]=rom[0xcbc5]=255;
            _runtimeState.SetWramByte(0xcbc3,255);_runtimeState.SetWramByte(0xcbc4,255);_runtimeState.SetWramByte(0xcbc5,255);
            var screen=_scene.EndingScreen;var sounds=_sound.AttachPlayRequestAudit();int update=0,generation=0,hold=0,texts=0;
            Texture2D? previousBackground=null;Texture2D[]? previousCells=null;bool sawLeft=false,sawRight=false,sawFlash=false;
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateGameplay(0,0,255,++update);
                rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(owner.State!=rom[0xcbc2]||owner.Stage!=rom[0xcbc1]||owner.CreditsPending!=(rom[0xc2ef]==10)||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.Visible||_hud.Visible||!screen.Visible||rom[0xd000]!=0||
                    _entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||rom[0xd240]!=0,
                    $"Ending screens update{update}: state={owner.Stage}:{owner.State}/{rom[0xcbc1]}:{rom[0xcbc2]}, credits={owner.CreditsPending}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}.");
                foreach(int address in new[]{0xcbb3,0xcbb5,0xcbba,0xcbc3,0xcbc4,0xcbc5,0xcbc9,WramAddress.wMenuDisabled,WramAddress.wOpenedMenuType,WramAddress.wTilesetAnimation})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Ending screens byte${address:x4} update{update}: port=${_runtimeState.ReadWramByte(address):x2}, ROM=${rom[address]:x2}.");
                int emitted=0;
                for(int index=0;index<screen.Parts.Count;index++)
                {
                    var part=screen.Parts[index];
                    if(((part.Y+screen.OamOffset.Y)&255)>=160||((part.X+screen.OamOffset.X)&255)>=168)continue;
                    int at=0xcb00+emitted++*4;
                    FailIf(rom[at]!=((part.Y+screen.OamOffset.Y)&255)||rom[at+1]!=((part.X+screen.OamOffset.X)&255)||
                        rom[at+2]!=part.Tile||rom[at+3]!=part.Flags,$"Ending OAM record{index} update{update}: port=${(part.Y+screen.OamOffset.Y)&255:x2},${(part.X+screen.OamOffset.X)&255:x2},${part.Tile:x2},${part.Flags:x2}; ROM=${rom[at]:x2},${rom[at+1]:x2},${rom[at+2]:x2},${rom[at+3]:x2}, OAMtail=${rom[0xff9f]:x2}.");
                }
                if(!ReferenceEquals(previousBackground,screen.BackgroundTexture))
                {
                    previousBackground=screen.BackgroundTexture;using var image=previousBackground!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map);
                        int source=0x9000+unchecked((sbyte)tile)*16,bank=(flags>>3)&1;
                        int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8;
                        int shade=((rom.VramByte(bank,source+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,source+sy*2+1)>>(7-sx))&1)<<1);
                        Color observed=image.GetPixel(x,y);int rgb=PackedRgb(observed),expected=menu.HudColor(false,flags&7,shade);
                        FailIf(rgb!=expected||observed.A!=1,$"Ending background pixel({x},{y}) update{update}: port=${rgb:x4}, ROM=${expected:x4}, tile${tile:x2}, flags${flags:x2}, shade{shade}.");
                    }
                }
                var cells=SomariaPrivate<Texture2D[]>(screen,"_cells");
                if(!ReferenceEquals(previousCells,cells))
                {
                    previousCells=cells;
                    for(int index=0;index<cells.Length;index++)
                    {
                        var part=screen.Parts[index];using var image=cells[index].GetImage();
                        for(int y=0;y<16;y++)for(int x=0;x<8;x++)
                        {
                            int sx=(part.Flags&0x20)==0?x:7-x,sy=(part.Flags&0x40)==0?y:15-y;
                            int at=0x8000+(part.Tile&~1)*16+sy*2,bank=(part.Flags>>3)&1;
                            int shade=((rom.VramByte(bank,at)>>(7-sx))&1)|(((rom.VramByte(bank,at+1)>>(7-sx))&1)<<1);
                            Color observed=image.GetPixel(x,y);int rgb=PackedRgb(observed),expected=menu.HudColor(true,part.Flags&7,shade);
                            FailIf(shade==0?observed.A!=0:observed.A!=1||rgb!=expected,
                                $"Ending sprite{index} pixel({x},{y}) update{update}: port=${rgb:x4}/A{observed.A}, ROM=${expected:x4}/shade{shade}.");
                        }
                    }
                }
                if(generation!=rom.TextGeneration)
                {
                    int[] expected=[0x1712,0x2c0b,0x2c0c];
                    FailIf(texts>=3||rom.Word(0xcba2)!=expected[texts]||!_dialogue.IsOpen||_dialogue.TextboxFlagsForValidation!=1,
                        $"Ending fullscreen text{texts} differs from expected source identity/NOCOLORS flags: nativeTX${rom.Word(0xcba2):x4}.");
                    // Each source message starts with \pos(3), overriding
                    // cleared-Link automatic placement; GFX $04 has SCY=$00.
                    FailIf(_dialogue.Position.Y!=96||texts==1&&rom[0xcbb3]!=0xc2,
                        $"Fullscreen text{texts} must retain source position $03/SCY=$00 and the source's E=$c2 post-dialogue counter: Y={_dialogue.Position.Y}, counter=${rom[0xcbb3]:x2}.");
                    generation=rom.TextGeneration;texts++;hold=3;
                }
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    !sounds.Requests.SequenceEqual(rom.Sounds),$"Ending fullscreen RNG/SFX update{update}: {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                sawLeft|=owner.Stage==1&&owner.State==3;sawRight|=owner.Stage==1&&owner.State==5;sawFlash|=owner.Stage==1&&owner.State==8;
            }
            while(update<1800&&!owner.CreditsPending&&!(cancelEarly&&owner.Stage==1&&owner.State==3))
                StepGameplayUpdates(owner.Stage==1&&owner.State>=8?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(!cancelEarly&&(!owner.CreditsPending||texts!=3||!sawLeft||!sawRight||!sawFlash||_currentRoom.Layout.Any(value=>value!=0)||_currentRoom.Collisions.Any(value=>value!=0)),
                "The normal-game fullscreen ending must preserve all three texts, both OAM streams, flash and cleared-terrain credits handoff.");
            owner.Cancel();
            FailIf(screen.Visible||screen.BackgroundTexture is not null||!_hud.Visible||!_currentRoom.Layout.SequenceEqual(layoutBefore)||
                !_currentRoom.Collisions.SequenceEqual(collisionsBefore)||_entities.NativeObjectPalettes is not null||
                !PalettesEqual(backgroundBefore,_world.BackgroundPalettes.Capture()),
                "Cancelling the fullscreen ending must restore shared palettes, HUD, room terrain and its stable presentation node.");
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
        }
        static int PackedRgb(Color value)=>(Mathf.RoundToInt(value.R*31))|(Mathf.RoundToInt(value.G*31)<<5)|(Mathf.RoundToInt(value.B*31)<<10);
        static bool PalettesEqual(Color[,] left,Color[,] right)=>left.Cast<Color>().SequenceEqual(right.Cast<Color>());
    }
}
