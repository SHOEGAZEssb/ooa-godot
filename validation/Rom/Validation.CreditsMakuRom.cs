using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerCreditsMakuRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int cancelPhase in new[]{0,1,2})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_inventory.SetScriptedHealth(4);_inventory.SetActiveRingEnabled(false);
            var palettesBefore=_world.BackgroundPalettes.Capture();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();var ownerType=typeof(BlackTowerEndingEvent);
            ownerType.GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            ownerType.GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            // The preceding fullscreen scene clears WRAM1, including the
            // disabled Link object's state/animation bytes, not only enabled.
            for(int address=0xd000;address<0xd080;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,0),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            var screen=_scene.EndingScreen;var sounds=_sound.AttachPlayRequestAudit();int update=0;
            bool sawSwirl=false,sawFlash=false,sawImpaJumps=false,sawRalphJump=false,sawLinkJump=false,sawCompleted=false;
            Texture2D? previousBackground=null;bool checkedStatuePixels=false;
            IntroOamPart[]? previousLinkParts=null;int previousLinkOam=-1;
            var sourceBytes=ValidationRom.LoadCleanUs();
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||owner.Stage!=1||_rooms.ActiveGroup!=0||_rooms.CurrentRoom.Id!=0x38||rom[0xcc2d]!=0||rom[0xcc30]!=0x38||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_hud.Visible||screen.Visible,
                    $"Maku credits update{update}: state{owner.State}/{rom[0xcbc2]}, room${_rooms.ActiveGroup:x}:${_rooms.CurrentRoom.Id:x2}, fade{_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}.");
                foreach(int address in new[]{0xcfc0,0xcfd5,0xcfd6,0xcfde,0xcfdf,0xcbb3,0xcbba,0xcbc3,0xcbc4,0xcbc5,0xcbc9,WramAddress.wLoadedTreeGfxIndex,WramAddress.wInteractionIDToLoadExtraGfx,WramAddress.wTilesetAnimation})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Maku credits byte${address:x4} update{update}: port${_runtimeState.ReadWramByte(address):x2}/ROM${rom[address]:x2}.");
                var impa=_entities.EntityAdapters<ImpaCreditsRoomEntity>().Single();
                int impaAt=0xd040+_entities.InteractionSlot(impa.Node)*256;
                FailIf(impaAt!=0xd240||impa.State!=rom[impaAt+4]||impa.Substate!=rom[impaAt+5]||
                    impa.Substate<3&&impa.Counter!=rom[impaAt+6]||impa.Substate is >=4 and <=6&&impa.Counter!=rom[impaAt+6]||
                    impa.Substate is >=4 and <=6&&impa.Counter2!=rom[impaAt+7]||impa.JumpWait!=rom[impaAt+0x38]||impa.ZFixed!=unchecked((short)rom.Word(impaAt+14))||
                    impa.SpeedZ!=unchecked((short)rom.Word(impaAt+20))||impa.PrecisePosition!=new Vector2(rom.Word(impaAt+12)/256f,rom.Word(impaAt+10)/256f),
                    $"Credits Impa update{update}: port{impa.State}:{impa.Substate} count{impa.Counter}/{impa.Counter2}/{impa.JumpWait} pos{impa.PrecisePosition} Z{impa.ZFixed}/{impa.SpeedZ}; ROM{rom[impaAt+4]}:{rom[impaAt+5]} count{rom[impaAt+6]}/{rom[impaAt+7]}/{rom[impaAt+0x38]} pos{rom.Word(impaAt+12)/256f},{rom.Word(impaAt+10)/256f} Z{unchecked((short)rom.Word(impaAt+14))}/{unchecked((short)rom.Word(impaAt+20))}.");
                CompareActor(impa.Actor,impaAt);
                foreach(var actor in _entities.EntityAdapters<NayruRescueActorRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(rom[at+1]!=actor.Id||rom[at+2]!=actor.SubId||actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||
                        actor.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||actor.ZFixed!=unchecked((short)rom.Word(at+14)),
                        $"Credits actor${actor.Id:x2}:${actor.SubId:x2} update{update}: port{actor.State}:{actor.Substate}/{actor.PrecisePosition}/Z{actor.ZFixed}; ROM{rom[at+4]}:{rom[at+5]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/Z{unchecked((short)rom.Word(at+14))}.");
                    CompareActor(actor.Actor,at);
                    sawRalphJump|=actor.Id==0x37&&actor.ZFixed!=0;
                }
                var statue=_entities.EntityAdapters<MakuLinkStatueRoomEntity>().Single();
                int statueAt=0xd040+_entities.InteractionSlot(statue.Node)*256;
                FailIf(statueAt!=0xd640||!statue.Initialized||rom[statueAt+1]!=0x6b||rom[statueAt+2]!=14,
                    "Credits statue must retain original slot $d6 and unconditionally initialize $6b:$0e.");
                CompareActor((NpcCharacter)statue.Node,statueAt);
                if(!checkedStatuePixels&&!_transitions.NativePaletteFadeActive)
                {
                    CompareInteractionSpritePixelsRom(rom,(NpcCharacter)statue.Node,statueAt,9,"Maku credits Link statue $6b:$0e");
                    checkedStatuePixels=true;
                }
                if(update==1)
                {
                    FailIf(_player.CreditsLinkState!=0||rom[0xd000]!=3||rom[0xd001]!=8||rom[0xd002]!=9||rom[0xd400]!=0||
                        _entities.EntityAdapters<CreditsLinkLoaderRoomEntity>().Any(),"The $a8:$64 interaction must publish Link $08:$09 and free its slot after Link's own pass.");
                }
                else
                {
                    FailIf(_player.CreditsLinkState!=rom[0xd004]||_player.CreditsLinkSubstate!=rom[0xd005]||_player.Visible!=((rom[0xd01a]&128)!=0)||
                        _player.CreditsLinkCounter!=rom[0xd006]||_player.CreditsLinkSpeedZ!=unchecked((short)rom.Word(0xd014))||
                        _player.CreditsLinkAnimationCounter!=rom[0xd020]||_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                        SomariaPrivate<int>(_player,"_topDownAirZFixed")!=unchecked((short)rom.Word(0xd00e)),
                        $"Credits Link update{update}: port{_player.CreditsLinkState}:{_player.CreditsLinkSubstate} count{_player.CreditsLinkCounter} anim{_player.CreditsLinkAnimation}/{_player.CreditsLinkAnimationCounter} Z{SomariaPrivate<int>(_player,"_topDownAirZFixed")}/{_player.CreditsLinkSpeedZ}; ROM{rom[0xd004]}:{rom[0xd005]} count{rom[0xd006]} anim{rom[0xd030]}/{rom[0xd020]} Z{unchecked((short)rom.Word(0xd00e))}/{unchecked((short)rom.Word(0xd014))}.");
                    var frame=_player.CutsceneSpriteFrame!.Value;int pointer=rom.Word(0xd01e);
                    // loadLinkAndCompanionAnimationFrame returns for hidden
                    // Link, retaining the previous object's VRAM/OAM pointer.
                    if(_player.Visible&&(!ReferenceEquals(previousLinkParts,frame.Parts)||previousLinkOam!=pointer))
                    {
                        previousLinkParts=frame.Parts;previousLinkOam=pointer;
                        int at=(0x13+(pointer>>14))*0x4000+(pointer&0x3fff);
                        var bytes=sourceBytes.Span;
                        FailIf(bytes[at++]!=frame.Parts.Length,"Credits Link OAM count differs from the original bank-$13 data.");
                        var renderer=SomariaPrivate<CutsceneSpriteRenderer>(_player,"_cutsceneSpriteRenderer");
                        foreach(var part in frame.Parts)
                        {
                            int nativeY=bytes[at++],nativeX=bytes[at++],tile=bytes[at++]+rom[0xd01d],flags=bytes[at++]^rom[0xd01c];
                            // The shared renderer addresses the imported Link
                            // sheet directly; native VRAM bank1 is resolved here.
                            FailIf(part.Y!=nativeY||part.X!=nativeX||(part.Flags&~8)!=(flags&~8),$"Credits Link OAM update{update} pointer${pointer:x4}: port{part.Y},{part.X},${part.Flags:x2}/ROM{nativeY},{nativeX},${flags:x2} (objectflags${rom[0xd01c]:x2}).");
                            using var image=renderer.CellTexture(frame,part).GetImage();
                            for(int y=0;y<16;y++)for(int x=0;x<8;x++)
                            {
                                int sx=(flags&0x20)==0?x:7-x,sy=(flags&0x40)==0?y:15-y;
                                int address=0x8000+(tile&~1)*16+sy*2,bank=(flags>>3)&1;
                                int shade=((rom.VramByte(bank,address)>>(7-sx))&1)|(((rom.VramByte(bank,address+1)>>(7-sx))&1)<<1);
                                var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                                FailIf(shade==0?color.A!=0:color.A!=1||rgb!=menu.HudColor(true,flags&7,shade),
                                    $"Credits Link pixel({x},{y}) update{update} differs from native VRAM tile${tile:x2}/flags${flags:x2}.");
                            }
                        }
                    }
                    FailIf(_transitions.WorldToScreen(_player.PrecisePosition).Floor()!=new Vector2((rom[0xd00d]-rom[0xffac])&255,(rom[0xd00b]-rom[0xffaa]+16)&255),
                        "Credits Link must retain world coordinates while matching the source's object draw projection.");
                }
                if(!ReferenceEquals(previousBackground,screen.BackgroundTexture))
                {
                    previousBackground=screen.BackgroundTexture;using var image=previousBackground!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map);
                        var tiles=SomariaPrivate<byte[]>(screen,"_map");var attributes=SomariaPrivate<byte[]>(screen,"_attributes");
                        FailIf(tiles[map-0x9800]!=tile||attributes[map-0x9800]!=flags,$"Credits Maku map ${map:x4}: port${tiles[map-0x9800]:x2},${attributes[map-0x9800]:x2}/ROM${tile:x2},${flags:x2}.");
                        int source=0x9000+unchecked((sbyte)tile)*16,bank=(flags>>3)&1;
                        int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8;
                        int shade=((rom.VramByte(bank,source+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,source+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        if(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade))
                        {
                            var vram=SomariaPrivate<OracleVramTileMap>(screen,"_tiles");vram.TryResolve(bank,tile,out var sourceImage,out int sourceTile);
                            int columns=sourceImage.GetWidth()/8;
                            var gray=sourceImage.GetPixel(sourceTile%columns*8+sx,sourceTile/columns*8+sy);
                            var palette=_world.BackgroundPalettes.Capture();
                            FailIf(true,$"Credits Maku background({x},{y}) update{update} differs: tile${tile:x2},flags${flags:x2},shade{shade}, sourceTile{sourceTile}/gray{gray.R}, port${rgb:x4}/ROM${menu.HudColor(false,flags&7,shade):x4}; palettePort[{string.Join(',',Enumerable.Range(0,4).Select(s=>palette[flags&7,s]))}], paletteROM[{string.Join(',',Enumerable.Range(0,4).Select(s=>menu.HudColor(false,flags&7,s)))}], tileset${_currentRoom.TilesetId:x2},animation{_currentRoom.AnimationGroup},nativegfx${rom[0xcd21]:x2},unique${rom[0xcd20]:x2}.");
                        }
                    }
                }
                int nativeBeads=Enumerable.Range(0,16).Count(slot=>rom[0xd0c0+slot*256]!=0&&rom[0xd0c1+slot*256]==0x53);
                FailIf(_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Count()!=nativeBeads,$"Credits energy swirl update{update}: port{_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Count()}/ROM{nativeBeads} allocations.");
                var flower=_entities.EntityAdapters<NayruRescueFlowerRoomEntity>().Single();int flowerAt=0xd040+_entities.InteractionSlot(flower.Node)*256;
                FailIf(flowerAt!=0xd440||rom[flowerAt+1]!=0x86,"The Maku flower must reuse the earlier freed $a8 slot and wait until the next object pass.");
                sawSwirl|=nativeBeads>0;sawFlash|=impa.Substate==2;sawImpaJumps|=impa.ZFixed!=0;
                sawLinkJump|=_player.CreditsLinkSubstate==3;sawCompleted|=owner.State==2;
                var rng=_random.CaptureState();FailIf(rng.Rng1!=rom[0xff94]||rng.Rng2!=rom[0xff95]||rng.Calls-seed.Calls!=rom.RandomCalls||
                    !sounds.Requests.SequenceEqual(rom.Sounds),$"Credits Maku sound/RNG update{update}: port{rng.Calls-seed.Calls}/{string.Join(',',sounds.Requests)}, ROM{rom.RandomCalls}/{string.Join(',',rom.Sounds)}.");
            }
            void CompareActor(NpcCharacter actor,int at)
            {
                FailIf(actor.CurrentAnimationCounter!=rom[at+0x20]||actor.CurrentAnimationParameter!=rom[at+0x21]||actor.Visible!=((rom[at+0x1a]&128)!=0)||
                    actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                    $"Credits actor${rom[at+1]:x2}:${rom[at+2]:x2} slot${at:x4} update{update}: anim{actor.CurrentAnimationCounter}/{actor.CurrentAnimationParameter},visible{actor.Visible},priority{actor.ZIndex}; ROM{rom[at+0x20]}/{rom[at+0x21]},visible${rom[at+0x1a]:x2}.");
            }
            while(update<1800&&!sawCompleted&&!(cancelPhase==1&&sawImpaJumps)&&!(cancelPhase==2&&sawSwirl))
                StepGameplayUpdates(update==0||_player.CreditsLinkSubstate==4&&_player.CreditsLinkCounter<=4?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(!checkedStatuePixels,"Maku credits must compare the visible statue's pixels with native OBJ graphics and palette $06.");
            FailIf(!sawSwirl||cancelPhase!=2&&(!sawFlash||!sawImpaJumps)||cancelPhase==0&&(!sawRalphJump||!sawLinkJump||!sawCompleted||rom[0xcfc0]!=6||rom[0xcfdf]!=255),
                "The native Maku credits must complete the warp, Impa's jumps, Ralph's statue reaction, Link's jump and final $cfdf signal.");
            if(cancelPhase==0)
            {
                while(owner.State==2)
                    StepGameplayUpdates(1,Vector2.Zero,batched:batched,afterUpdate:()=>
                    {
                        rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                        FailIf(owner.State!=rom[0xcbc2]||_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0),"Maku-to-caption handoff must wait for the complete white fade.");
                    });
                FailIf(owner.State!=3||rom[0xcbb3]!=90||_runtimeState.ReadWramByte(0xcbb3)!=90||rom[0xcfdf]!=0||
                    _runtimeState.ReadWramByte(0xcfdf)!=0||!screen.Visible||_player.Visible||_player.CreditsLinkState!=-1||
                    _entities.EntityAdapters<ImpaCreditsRoomEntity>().Any()||_entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||
                    _entities.EntityAdapters<NayruRescueFlowerRoomEntity>().Any()||_entities.EntityAdapters<MakuLinkStatueRoomEntity>().Any()||
                    Enumerable.Range(0,16).Any(slot=>rom[0xd040+slot*256]!=0)||rom[0xd000]!=0,
                    "The first caption card must clear the entire previous object bank, hide Link, reset $cfdf and begin its 90-update hold.");
            }
            owner.Cancel();
            FailIf(_player.CreditsLinkState!=-1||_player.CutsceneSpriteFrame is not null||_entities.EntityAdapters<ImpaCreditsRoomEntity>().Any()||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||_entities.EntityAdapters<NayruRescueFlowerRoomEntity>().Any()||
                _entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any()||
                !_hud.Visible||!palettesBefore.Cast<Color>().SequenceEqual(_world.BackgroundPalettes.Capture().Cast<Color>()),
                "Cancelling the Maku credits must release Link, all scene actors, presentation and shared palettes.");
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
        }
    }
}
