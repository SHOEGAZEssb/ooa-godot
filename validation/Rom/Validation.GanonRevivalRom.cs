using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGanonRevivalRom()
    {
        var animationCounter=typeof(EnemyAnimationPlayer).GetField("_frameCounter",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(bool batched in new[] {false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            foreach(var enemy in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray())_entities.RetireEntity(enemy);
            _entities.SetNativeRoomEnemyCount(2);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);_runtimeState.SetWramByte(WramAddress.wDisableLinkCollisionsAndMenu,1);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78){HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false,CompanionDispatchEnabled=true};
            rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();
            rom[0xcdd1]=2;rom[0xcc8a]=1;rom[0xcbca]=1;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            rom[0xc2ef]=0x19;rom[0xcc03]=0;
            var owner=_roomEvents.Get<RoomOfRitesBattleEvent>();owner.Begin(0x19);
            var sounds=_sound.AttachPlayRequestAudit();int update=0,shadows=0;bool terminal=false;
            var seen=new HashSet<GanonRevivalRoomEntity>();var phases=new HashSet<int>();
            do{StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:Compare);}while(!terminal&&update<1700);
            _sound.SetRequestObserver(null);
            FailIf(!terminal||shadows!=16||!phases.SetEquals(new[]{0,13,1,2,3}),
                $"Ganon revival missed its sixteen shadows, ball/skull/body/roar phases or battle dialogue; shadows={shadows},phases={string.Join(',',phases)}.");

            void Compare()
            {
                bool nativeCutscene=rom[0xc2ef]==0x19;
                if(nativeCutscene)rom.AdvanceTwinrovaFlameCutscene(_entities.FrameCounter,0x19);
                else{rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();}
                update++;
                FailIf(owner.NativeActive!=(rom[0xc2ef]==0x19)||owner.NativeActive&&owner.State!=rom[0xcc03]||_rooms.CurrentRoom.Id!=rom[0xcc30]||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0),
                    $"Sacrifice/revival update{update}: state={owner.Cutscene:x2}:{owner.State}/{rom[0xc2ef]:x2}:{rom[0xcc03]},room={_rooms.CurrentRoom.Id:x2}/{rom[0xcc30]:x2},fade={_transitions.NativePaletteThreadMode:x2}/{rom[0xc4ab]:x2}.");
                FailIf(_player.Position!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||_player.Visible!=((rom[0xd01a]&128)!=0),
                    $"Sacrifice/revival update{update}: Link={_player.Position}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f},visible={_player.Visible}/{rom[0xd01a]:x2},nativeLink={rom[0xd000]:x2}:{rom[0xd001]:x2}:{rom[0xd004]:x2}:{rom[0xd005]:x2},boss={rom[0xd084]:x2},cutscene={nativeCutscene},SVBK={rom[0xff70]:x2}.");
                for(int slot=0;slot<16;slot++)
                {
                    int at=0xd080+slot*256;var entity=_entities.ResolveEnemySlot(slot);
                    FailIf((entity is GanonRoomEntity or GanonRevivalRoomEntity)!=(rom[at]!=0&&rom[at+1] is 4 or 0x60),$"Revival update{update}: ENEMY slot${slot:x2} admission/lifetime differs.");
                    if(entity is GanonRoomEntity boss)
                    {
                        var a=boss.Actor;
                        FailIf(a.State!=rom[at+4]||a.Counter!=rom[at+6]||a.Counter2!=rom[at+7]||a.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                            a.ZFixed!=unchecked((short)rom.Word(at+14))||a.Visible!=((rom[at+0x1a]&128)!=0)||a.Health!=rom[at+0x29]||
                            (int)animationCounter.GetValue(a.Animation)! !=rom[at+0x20]||a.AnimationParameter!=rom[at+0x21],
                            $"Ganon revival update{update}: state={a.State}/{rom[at+4]},counter={a.Counter}/{rom[at+6]},counter2={a.Counter2}/{rom[at+7]},visible={a.Visible}/{rom[at+0x1a]:x2},anim={a.AnimationIndex}:{animationCounter.GetValue(a.Animation)}/{rom[at+0x20]}.");
                        // Compare base OAM pixels after the white fade has
                        // restored SPR colors; the following fade affects BG only.
                        if((a.AnimationIndex!=0||rom[0xc4ab] is 0 or 0x0b)&&phases.Add(a.AnimationIndex))
                        {
                            int cells=a.AnimationIndex switch {0=>10,13=>10,1=>4,2=>18,3=>18,_=>throw new InvalidOperationException("Unexpected revival animation.")};
                            CompareInteractionSpritePixelsRom(rom,a.CurrentDrawTexture,a.Animation.CurrentOffset,at,cells,$"Ganon revival animation${a.AnimationIndex:x2}",0x13);
                        }
                        terminal=a.State==7;
                        bool darkening=_entities.EntityAdapters<GanonRevivalRoomEntity>().Any(s=>s.Actor.Subid==0&&s.Actor.Initialized!=0);
                        if(darkening||a.State is >=3 and <=7)
                        {
                            var palette=rom.CreateMenuView();
                            for(int p=2;p<8;p++)for(int shade=0;shade<4;shade++)
                            {
                                int rgb=a.State<6?palette.FadingColor(false,p,shade):palette.HudColor(false,p,shade);var actual=_currentRoom.BackgroundPalettes.Resolve(p,shade);
                                FailIf(Math.Abs(actual.R-(rgb&31)/31f)>1f/255||Math.Abs(actual.G-(rgb>>5&31)/31f)>1f/255||Math.Abs(actual.B-(rgb>>10&31)/31f)>1f/255,
                                    $"Ganon BG-only fade update{update}: palette{p},shade{shade}={actual}/RGB555${rgb:x4},baseRGB=${palette.HudColor(false,p,shade):x4},tilesetPalette={_currentRoom.TilesetPaletteId:x2}/{rom[0xcd22]:x2},nativeMode={rom[0xc4ab]:x2}.");
                            }
                        }
                    }
                    else if(entity is GanonRevivalRoomEntity shadow)
                    {
                        var a=shadow.Actor;if(seen.Add(shadow)&&a.Subid==1)shadows++;
                        FailIf(a.Subid!=rom[at+2]||a.Initialized!=rom[at+0x31]||a.Interval!=rom[at+0x30]||a.Counter!=rom[at+6]||a.Counter2!=rom[at+7]||
                            a.Angle!=rom[at+9]||a.Speed!=rom[at+16]||a.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                            a.Visible!=((rom[at+0x1a]&128)!=0)||(int)animationCounter.GetValue(a.Animation)! !=rom[at+0x20],
                            $"Revival shadow update{update},slot${slot:x2}: initialized={a.Initialized}/{rom[at+0x31]},counter={a.Counter}/{rom[at+6]},counter2={a.Counter2}/{rom[at+7]},angle={a.Angle}/{rom[at+9]},pos={a.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}.");
                    }
                }
                FailIf(_entities.RoomEnemyCount!=rom[0xcdd1]||RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Sacrifice/revival update{update}: count={_entities.RoomEnemyCount}/{rom[0xcdd1]},RNG={_random.Calls-seed.Calls}/{rom.RandomCalls},sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
        }
        GD.Print("Validated sacrifice/lightning and native Ganon revival slots, sixteen shadows, frame timing, RNG/audio, Link handoff and executed VRAM/OBJ pixels against the clean ROM.");
    }
}
