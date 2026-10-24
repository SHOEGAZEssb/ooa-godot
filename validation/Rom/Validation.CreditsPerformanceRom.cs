using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCreditsPerformanceRom()
    {
        foreach(bool batched in new[]{false,true})foreach(bool cancelFlight in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            var before=_world.BackgroundPalettes.Capture();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            rom.LoadCutsceneGraphics(0x83,0x0f); // Common OBJ graphics remain resident throughout the normal ending.
            for(int address=0xd000;address<0xe000;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,1),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            var screen=_scene.EndingScreen;var sounds=_sound.AttachPlayRequestAudit();int update=0,maxNotes=0,maxLap=0;
            void CompareActor(NpcCharacter actor,int state,int at)
            {
                CompareFullscreenActorPositionRom(rom,actor,at,"Credits performance");
                FailIf(state!=rom[at+4]||actor.CurrentAnimationCounter!=rom[at+0x20]||actor.CurrentAnimationParameter!=rom[at+0x21]||
                    actor.Position.Floor()!=new Vector2(rom[at+13],rom[at+11])||actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a])||actor.Visible!=((rom[at+0x1a]&128)!=0),
                    $"Performance actor${rom[at+1]:x2}:${rom[at+2]:x2} update{update} slot${at:x4}: port {state}/anim{actor.CurrentAnimationCounter},{actor.CurrentAnimationParameter}/pos{actor.Position}/priority{actor.ZIndex}; ROM {rom[at+4]}/anim{rom[at+0x20]},{rom[at+0x21]}/pos{rom[at+13]},{rom[at+11]}/visible${rom[at+0x1a]:x2}.");
            }
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||owner.Stage!=rom[0xcbc1]||_rooms.ActiveGroup!=0||_currentRoom.Id!=0x3a||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.Visible||_hud.Visible||screen.Visible,
                    $"Performance update{update}: port{owner.Stage}:{owner.State}, ROM{rom[0xcbc1]}:{rom[0xcbc2]}.");
                foreach(int address in new[]{0xcfd0,0xcfde,0xcfdf,WramAddress.wTilesetAnimation,0xcec0,0xcec1,0xcec2,0xcec3})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Performance byte${address:x4} update{update}: port${_runtimeState.ReadWramByte(address):x2}/ROM${rom[address]:x2}.");
                foreach(var actor in _entities.EntityAdapters<NayruRescueActorRoomEntity>())CompareActor(actor.Actor,actor.State,0xd040+_entities.InteractionSlot(actor.Node)*256);
                foreach(var actor in _entities.EntityAdapters<ForestAudienceRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;CompareActor(actor.Actor,actor.State,at);
                    FailIf(unchecked((short)rom.Word(at+20))!=actor.SpeedZ,"Bird credits subids must retain their unused native jump speed without applying gravity.");
                }
                foreach(var actor in _entities.EntityAdapters<RestoredFamilyRoomEntity>())CompareActor(actor.Actor,actor.State,0xd040+_entities.InteractionSlot(actor.Node)*256);
                foreach(var actor in _entities.EntityAdapters<RestoredRabbitRoomEntity>())CompareActor(actor.Actor,actor.State,0xd040+_entities.InteractionSlot(actor.Node)*256);
                var maple=_entities.EntityAdapters<MapleCreditsRoomEntity>().Single().Maple;
                FailIf(rom[0xd100]!=1||rom[0xd101]!=0x12||rom[0xd102]!=0||maple.CreditsState!=rom[0xd104]||maple.Substate!=rom[0xd105]||
                    SomariaPrivate<int>(maple,"_counter")!=rom[0xd106]||maple.CreditsLap!=rom[0xd107]||
                    SomariaPrivate<Vector2>(maple,"_precisePosition")!=new Vector2(rom.Word(0xd10c)/256f,rom.Word(0xd10a)/256f)||
                    SomariaPrivate<int>(maple,"_zFixed")!=unchecked((short)rom.Word(0xd10e))||SomariaPrivate<int>(maple,"_speedZ")!=unchecked((short)rom.Word(0xd114)),
                    $"Credits Maple update{update}: port{maple.CreditsState}:{maple.Substate}/{SomariaPrivate<int>(maple,"_counter")}/{maple.CreditsLap}/pos{SomariaPrivate<Vector2>(maple,"_precisePosition")}/Z{SomariaPrivate<int>(maple,"_zFixed")}; ROM{rom[0xd104]}:{rom[0xd105]}/{rom[0xd106]}/{rom[0xd107]}/pos{rom.Word(0xd10c)/256f},{rom.Word(0xd10a)/256f}/Z{unchecked((short)rom.Word(0xd10e))}.");
                var animation=SomariaPrivate<EnemyAnimationPlayer>(maple,"_animation");
                FailIf(maple.AnimationIndex!=rom[0xd130]||SomariaPrivate<int>(animation,"_frameCounter")!=rom[0xd120],$"Maple animation update{update}: port{maple.AnimationIndex}/{SomariaPrivate<int>(animation,"_frameCounter")}, ROM{rom[0xd130]}/{rom[0xd120]}.");
                FailIf(SomariaPrivate<int>(maple,"_speedRaw")!=rom[0xd110]||maple.Angle!=rom[0xd109],"Maple credits must retain the source speed and alternating flight angle.");
                if(update==1)FailIf(maple.CreditsState!=0||maple.Visible||rom[0xd104]!=0,"The final $a8:$03 slot must defer Maple's first update until the next special-object pass.");
                int notes=_entities.EntityAdapters<NayruRescueMusicNoteRoomEntity>().Count();maxNotes=System.Math.Max(maxNotes,notes);maxLap=System.Math.Max(maxLap,maple.CreditsLap);
                int nativeNotes=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0xa0);
                FailIf(notes!=nativeNotes,$"Performance notes update{update}: port{notes}/ROM{nativeNotes}.");
                foreach(var note in _entities.EntityAdapters<NayruRescueMusicNoteRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(note.Node)*256;var actor=(NpcCharacter)note.Node;
                    FailIf(note.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||SomariaPrivate<int>(note,"_counter")!=rom[at+6],
                        $"Performance note update{update} slot${at:x4}: port{actor.Position}/{SomariaPrivate<int>(note,"_counter")}, ROM{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{rom[at+6]}.");
                    CompareActor(actor,1,at);
                }
                if(update is 1 or 64)
                {
                    using var image=screen.BackgroundTexture!.GetImage();
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=0x9800+y/8*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map),address=0x9000+unchecked((sbyte)tile)*16;
                        int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8,bank=(flags>>3)&1;
                        int shade=((rom.VramByte(bank,address+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,address+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Performance background update{update} pixel{x},{y} tile${tile:x2} differs from native animated scene $87.");
                    }
                }
                var rng=_random.CaptureState();FailIf(rng.Calls-seed.Calls!=rom.RandomCalls||rng.Rng1!=rom[0xff94]||rng.Rng2!=rom[0xff95]||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Performance sound/RNG update{update}: port{rng.Calls-seed.Calls}/{string.Join(',',sounds.Requests)}, ROM{rom.RandomCalls}/{string.Join(',',rom.Sounds)}.");
            }
            while(update<1800&&owner.State!=2&&!(cancelFlight&&maxLap==2))
                StepGameplayUpdates(update<2?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(update>=1800||maxNotes==0||maxLap!=(cancelFlight?2:4),"Nayru's credits performance must produce bounded notes and Maple's complete four-flight/final-descent signal.");
            if(!cancelFlight)
            {
                while(owner.State==2)StepGameplayUpdates(1,Vector2.Zero,batched:batched,afterUpdate:()=>
                {rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);FailIf(owner.State!=rom[0xcbc2],"Performance-to-caption handoff must wait for the source fade.");});
                FailIf(owner.State!=3||_runtimeState.ReadWramByte(0xcbb3)!=90||rom[0xcbb3]!=90||!screen.Visible||
                    _entities.EntityAdapters<MapleCreditsRoomEntity>().Any()||_entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||
                    _entities.EntityAdapters<ForestAudienceRoomEntity>().Any()||_entities.EntityAdapters<NayruRescueMusicNoteRoomEntity>().Any(),
                    "The second caption card must clear the performance's actors, companion and notes before its 90-update hold.");
            }
            owner.Cancel();
            FailIf(_entities.EntityAdapters<MapleCreditsRoomEntity>().Any()||_entities.EntityAdapters<NayruRescueMusicNoteRoomEntity>().Any()||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||_entities.EntityAdapters<ForestAudienceRoomEntity>().Any()||
                !_hud.Visible||_transitions.NativeRoomUsesFullScreen||!before.Cast<Color>().SequenceEqual(_world.BackgroundPalettes.Capture().Cast<Color>()),"Credits performance cancellation must retire all scene actors/notes/companion and restore shared palettes.");
        }
    }
}
