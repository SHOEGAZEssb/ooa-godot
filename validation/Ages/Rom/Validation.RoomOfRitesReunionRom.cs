using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRoomOfRitesReunionRom()
    {
        foreach(bool batch in new[]{false,true})
        foreach(bool shield in new[]{false,true})
        foreach(bool cancelAtKiss in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);_saveData.SetRoomFlag(5,0xf1,0x80);LoadValidationRoom(5,0xf1);
            // Cutscene room loads retire the rescue actors but retain their
            // room event through the $0f -> $0a handoff.
            var rescue=_roomEvents.Get<RoomOfRitesZeldaEvent>();
            FailIf(!rescue.HasState,"Reunion fixture must retain the room $5:$f1 rescue event.");
            _entities.Clear();
            _player.WarpTo(new(0x78,0x58));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(shield?TreasureId.Shield:0);_inventory.EquipB(0);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,0x11);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x58)
                {HostilePartsEnabled=true,PostObjectCollisionsEnabled=false,CompanionDispatchEnabled=true};
            rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom[0xcd17]=0xd0;
            rom[0xcc8a]=0x11;rom[0xc2ef]=0x0f;rom[0xcbc1]=1;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.BeginRoomOfRitesCollapse();
            // Stage only the independently traced reunion entry. The collapse
            // controller and the physical Zelda handoff have their own tests.
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(owner,false);
            _runtimeState.SetWramByte(0xcbc1,1);_runtimeState.SetWramByte(0xcbc2,0);
            var sounds=_sound.AttachPlayRequestAudit();int update=0,generation=0,hold=0;
            bool sawKiss=false,sawCard=false;int previousLinkAnimation=-1;bool checkedCard=false;
            var heartFrames=new HashSet<int>();
            while(!owner.CreditsPending&&update<2400&&!(cancelAtKiss&&sawKiss))
                StepGameplayUpdates(owner.State==10?1:3,Vector2.Zero,batched:batch,afterUpdate:Compare);
            if(!cancelAtKiss&&owner.CreditsPending)
                StepGameplayUpdates(3,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(!sawKiss||!cancelAtKiss&&(!owner.CreditsPending||!sawCard||!checkedCard),"Reunion must finish the six native actors, shield-selected Link poses and credits handoff.");
            owner.Cancel();
            FailIf(_player.CreditsLinkState!=-1||_entities.EntityAdapters<ZeldaEndingRoomEntity>().Any()||
                _entities.EntityAdapters<DinRoomEntity>().Any()||_entities.EntityAdapters<KissHeartRoomEntity>().Any()||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||_entities.EntityAdapters<NayruRescueFlowerRoomEntity>().Any()||
                _entities.EntityAdapters<ImpaCreditsRoomEntity>().Any(),"Cancelling reunion must retire its live actors, flower, hearts and special Link.");
            void Compare()
            {
                rom[0xcc00]=(byte)_entities.FrameCounter;rom.AdvanceWarpPalette();
                if(rom[0xc2ef]==0x0a)rom.AdvanceBlackTowerEnding();else rom.AdvanceRoomOfRitesCollapse();
                rom.UpdateGameplay(0,0,255,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_runtimeState.ReadWramByte(0xcbb3)!=rom[0xcbb3]||
                    _runtimeState.ReadWramByte(0xcbb4)!=rom[0xcbb4]||_runtimeState.ReadWramByte(0xcfc0)!=rom[0xcfc0]||
                    _rooms.ActiveGroup!=rom[0xcc2d]||_currentRoom.Id!=rom[0xcc30]||_transitions.NativePaletteThreadMode!=rom[0xc4ab],
                    $"Reunion update{update}: state={owner.Stage}:{owner.State}/{rom[0xcbc1]}:{rom[0xcbc2]},counter={_runtimeState.ReadWramByte(0xcbb3)}/{rom[0xcbb3]},signal={_runtimeState.ReadWramByte(0xcfc0)}/{rom[0xcfc0]},room={_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}/{rom[0xcc2d]:x}:{rom[0xcc30]:x2},fade={_transitions.NativePaletteThreadMode}/{rom[0xc4ab]}.");
                foreach(var actor in _entities.EntityAdapters<NayruRescueActorRoomEntity>())CheckActor(actor.Actor);
                foreach(var actor in _entities.EntityAdapters<ImpaCreditsRoomEntity>())CheckActor(actor.Actor);
                foreach(var actor in _entities.EntityAdapters<ZeldaEndingRoomEntity>())
                {
                    CheckActor(actor.Actor);int at=0xd040+_entities.NativeInteractionSlot(actor)*256;
                    FailIf(actor.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f),"Reunion Zelda must retain native movement fractions.");
                }
                foreach(var actor in _entities.EntityAdapters<DinRoomEntity>())
                {
                    CheckActor(actor.Actor);int at=0xd040+_entities.NativeInteractionSlot(actor)*256;
                    FailIf(actor.Substate!=rom[at+5]||actor.Counter!=rom[at+6]||actor.ZFixed!=unchecked((short)rom.Word(at+14))||actor.SpeedZ!=unchecked((short)rom.Word(at+20)),
                        $"Reunion Din update{update}: substate/counter/Z/speedZ differs.");
                }
                foreach(var heart in _entities.EntityAdapters<KissHeartRoomEntity>())
                {
                    sawKiss=true;CheckActor(heart.Actor);
                    int at=0xd040+_entities.NativeInteractionSlot(heart)*256,pointer=rom.Word(at+0x1e);
                    int count=ValidationRom.LoadCleanUs().Span[0x14*0x4000+(pointer&0x3fff)];
                    if(count!=0&&heartFrames.Add(heart.Subid*65536+pointer))
                        CompareInteractionSpritePixelsRom(rom,heart.Actor,at,count,$"Reunion kiss-heart${heart.Subid:x2},update{update}");
                }
                if(_player.CreditsLinkState>=0&&update>1)
                {
                    FailIf(_player.CreditsLinkState!=rom[0xd004]||_player.CreditsLinkSubstate!=rom[0xd005]||
                        _player.CreditsLinkCounter!=rom.Word(0xd006)||_player.CreditsLinkAnimationCounter!=rom[0xd020]||
                        _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||_player.Visible!=((rom[0xd01a]&128)!=0),
                        $"Reunion Link update{update}: state={_player.CreditsLinkState}:{_player.CreditsLinkSubstate}/{rom[0xd004]}:{rom[0xd005]},counter={_player.CreditsLinkCounter}/{rom.Word(0xd006)},anim={_player.CreditsLinkAnimationCounter}/{rom[0xd020]}.");
                    if(previousLinkAnimation!=_player.CreditsLinkAnimation)
                    {
                        previousLinkAnimation=_player.CreditsLinkAnimation;CheckLinkOam();
                    }
                }
                sawCard|=owner.Stage==1&&owner.State>=9;
                if(sawCard&&!checkedCard&&!_transitions.NativePaletteFadeActive)
                {
                    checkedCard=true;var screen=_scene.EndingScreen;
                    using var image=screen.BackgroundTexture!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map);
                        int at=0x9000+unchecked((sbyte)tile)*16,bank=(flags>>3)&1;
                        int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8;
                        int shade=((rom.VramByte(bank,at+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,at+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);
                        FailIf(color.A!=1||Rgb(color)!=menu.HudColor(false,flags&7,shade),$"Reunion card background({x},{y}) differs from native GFXH $3c.");
                    }
                    var cells=SomariaPrivate<Texture2D[]>(screen,"_cells");int emitted=0;
                    for(int index=0;index<cells.Length;index++)
                    {
                        var part=screen.Parts[index];int at=0xcb00+emitted++*4;
                        FailIf(rom[at]!=((part.Y+screen.OamOffset.Y)&255)||rom[at+1]!=((part.X+screen.OamOffset.X)&255)||
                            rom[at+2]!=part.Tile||rom[at+3]!=part.Flags,"Reunion card must emit native OAM $16:4f73 at offset $3038.");
                        CheckPixels(cells[index],part.Tile,part.Flags,"Reunion card");
                    }
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Reunion update{update}: RNG/audio differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=3;}
                if(hold!=0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
            }
            void CheckActor(NpcCharacter actor)
            {
                int at=0xd040+_entities.InteractionSlot(actor)*256;
                CompareInteractionOamRom(rom,actor,at,$"Reunion update{update} ${actor.Record.Id:x2}:${actor.Record.SubId:x2}");
                FailIf(actor.CurrentAnimationCounter!=rom[at+0x20]||actor.CurrentAnimationParameter!=rom[at+0x21]||
                    actor.Position!=new Vector2(rom[at+13],rom[at+11])||actor.Visible!=((rom[at+0x1a]&128)!=0),
                    $"Reunion update{update} actor${actor.Record.Id:x2}:${actor.Record.SubId:x2}: pos={actor.Position}/{rom[at+13]},{rom[at+11]},anim={actor.CurrentAnimationCounter}/{rom[at+0x20]},param={actor.CurrentAnimationParameter}/{rom[at+0x21]},visible={actor.Visible}/{rom[at+0x1a]:x2}.");
            }
            void CheckLinkOam()
            {
                var frame=_player.CutsceneSpriteFrame!.Value;int pointer=rom.Word(0xd01e),at=(0x13+(pointer>>14))*0x4000+(pointer&0x3fff);
                var bytes=ValidationRom.LoadCleanUs().Span;
                FailIf(bytes[at++]!=frame.Parts.Length,"Reunion Link native OAM count differs.");
                foreach(var part in frame.Parts)
                {
                    int y=bytes[at++],x=bytes[at++],tile=bytes[at++]+rom[0xd01d],flags=bytes[at++]^rom[0xd01c];
                    FailIf(part.Y!=y||part.X!=x||(part.Flags&~8)!=(flags&~8),"Reunion Link shield/kiss pose lost its source OAM.");
                    if(_player.Visible)CheckPixels(SomariaPrivate<CutsceneSpriteRenderer>(_player,"_cutsceneSpriteRenderer").CellTexture(frame,part),tile,flags,"Reunion Link");
                }
            }
            void CheckPixels(Texture2D texture,int tile,int flags,string label)
            {
                using var image=texture.GetImage();
                for(int y=0;y<16;y++)for(int x=0;x<8;x++)
                {
                    int sx=(flags&0x20)==0?x:7-x,sy=(flags&0x40)==0?y:15-y;
                    int at=0x8000+(tile&~1)*16+sy*2,bank=(flags>>3)&1;
                    int shade=((rom.VramByte(bank,at)>>(7-sx))&1)|(((rom.VramByte(bank,at+1)>>(7-sx))&1)<<1);
                    var color=image.GetPixel(x,y);
                    FailIf(shade==0?color.A!=0:color.A!=1||Rgb(color)!=menu.HudColor(true,flags&7,shade),$"{label} pixel({x},{y}) tile${tile:x2}/flags${flags:x2} differs from native VRAM.");
                }
            }
            static int Rgb(Color color)=>Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
        }
        GD.Print("Validated reunion actor scripts, Din jumps, Link's shield/no-shield kiss poses, native signal/counter/slot/OAM/audio ordering and credits handoff in split/batched gameplay against the clean ROM.");
    }
}
