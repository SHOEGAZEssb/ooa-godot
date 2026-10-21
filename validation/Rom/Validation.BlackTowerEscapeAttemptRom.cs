using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private async Task ValidateBlackTowerEscapeAttemptRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(4,0xfc,0x80);LoadValidationRoom(4,0xfc);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x78,0x58));_player.Face(Vector2I.Down);_transitions.ResetCamera();
            var owner=_roomEvents.Get<BlackTowerEscapeAttemptEvent>();owner.Begin();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x58)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom[0xc2ef]=0x1f;rom[0xcc03]=0;rom[0xcc46]=(byte)_sound.NativeRoomMusic;
            rom[0xcc05]=0xff; // initializeGame's parser mask admits the destination's placed $2d interaction.
            rom[0xcd16]=0;rom[0xcd17]=0xd0;rom[0xffaa]=24;rom[0xffac]=40;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,generation=0,hold=0,texts=0;bool sawRocks=false,sawHand=false,sawF3=false;
            _scene.ProcessMode=ProcessModeEnum.Disabled;
            NpcCharacter? drawnHand=null;int handDraws=0,drawnZ=0,drawCheckpoints=0;
            void CaptureHandDraw(){handDraws++;drawnZ=(int)drawnHand!.ScriptDrawOffset.Y;}
            try
            {
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEscapeAttempt();rom.UpdateGameplay(0,0,0xff,++update);
                rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(owner.State!=rom[0xcc03]||_rooms.ActiveGroup!=rom[0xcc2d]||_rooms.CurrentRoom.Id!=rom[0xcc30]||
                    _runtimeState.ReadWramByte(0xcbb4)!=rom[0xcbb4]||_runtimeState.ReadWramByte(0xcbb5)!=rom[0xcbb5]||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    CarriedObjectMotion.DirectionIndex(_player.FacingVector)!=rom[0xd008]||_player.Visible!=((rom[0xd01a]&128)!=0),
                    $"Black Tower escape $1f update {update}, batch={batched}: port state/room/timer/signal/Link/dir/visible="+
                    $"{owner.State}/{_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}/{_runtimeState.ReadWramByte(0xcbb4)}/{_runtimeState.ReadWramByte(0xcbb5)}/{_player.PrecisePosition}/{_player.FacingVector}/{_player.Visible}; "+
                    $"ROM={rom[0xcc03]}/{rom[0xcc2d]:x}:{rom[0xcc30]:x2}/{rom[0xcbb4]}/{rom[0xcbb5]}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}/{rom[0xd008]}/{rom[0xd01a]:x2}, LinkState={rom[0xd004]}:{rom[0xd005]}.");
                foreach(var actor in _entities.EntityAdapters<NayruRescueActorRoomEntity>())
                {
                    // Rendering samples terrain even while companions wait
                    // below the room at Y=$c8 (packed byte $c7).
                    _ = actor.Actor.CurrentTerrainEffect;
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(actor.Id!=rom[at+1]||actor.SubId!=rom[at+2]||actor.State!=rom[at+4]||
                        actor.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        actor.Actor.CurrentAnimationCounter!=rom[at+0x20],
                        $"Escape companion ${actor.Id:x2}:${actor.SubId:x2} slot ${at:x4} update {update}: port={actor.State}/{actor.PrecisePosition}/{actor.Actor.CurrentAnimationCounter}, ROM={rom[at+4]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{rom[at+0x20]}.");
                }
                foreach(var rock in _entities.EntityAdapters<FallingRockRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(rock.Node)*256;
                    FailIf(rock.SubId!=rom[at+2]||rock.State!=rom[at+4]||rock.Counter!=rom[at+6]||rock.Counter2!=rom[at+7]||
                        rock.Angle!=rom[at+9]||rock.Speed!=rom[at+16]||rock.ZFixed!=unchecked((short)rom.Word(at+14))||
                        rock.SpeedZ!=unchecked((short)rom.Word(at+20))||rock.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f),
                        $"Falling rock $92:{rock.SubId:x2} slot ${at:x4} update {update}: port={rock.State}/{rock.Counter},{rock.Counter2}/{rock.PrecisePosition}/{rock.Angle}/{rock.Speed}/{rock.ZFixed}/{rock.SpeedZ}; "+
                        $"ROM={rom[at+4]}/{rom[at+6]},{rom[at+7]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{rom[at+9]}/{rom[at+16]}/{unchecked((short)rom.Word(at+14))}/{unchecked((short)rom.Word(at+20))}.");
                    sawRocks=true;
                }
                foreach(var hand in _entities.EntityAdapters<VeranCutsceneWallmasterRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(hand.Node)*256;
                    var npc=(NpcCharacter)hand.Node;
                    if(drawnHand is null){drawnHand=npc;drawnHand.Draw+=CaptureHandDraw;}
                    FailIf(hand.State!=rom[at+4]||hand.Substate!=rom[at+5]||hand.Counter!=rom[at+6]||
                        hand.ZFixed!=unchecked((short)rom.Word(at+14))||hand.SpeedZ!=unchecked((short)rom.Word(at+20)),
                        $"Cutscene Wallmaster $2c slot ${at:x4} update {update}: port={hand.State}:{hand.Substate}/{hand.Counter}/{hand.ZFixed}/{hand.SpeedZ}, ROM={rom[at+4]}:{rom[at+5]}/{rom[at+6]}/{unchecked((short)rom.Word(at+14))}/{unchecked((short)rom.Word(at+20))}.");
                    FailIf(npc.Visible!=((rom[at+0x1a]&128)!=0)||npc.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a])||
                        npc.ScriptDrawOffset!=Vector2.Down*unchecked((sbyte)rom[at+15])||
                        ((ITerrainShadowSource)npc).TerrainShadowZHigh!=unchecked((sbyte)rom[at+15])||
                        npc.CurrentAnimationCounter!=rom[at+0x20]||npc.CurrentAnimationParameter!=rom[at+0x21],
                        $"Wallmaster $2c update{update}: visible/priority/draw height/terrain bit/animation differs from native visible${rom[at+0x1a]:x2} (port visible={npc.Visible}).");
                    sawHand=true;
                }
                foreach(int id in new[]{0x36,0x37,0x92,0x2c})
                {
                    int port=id switch
                    {
                        0x36 or 0x37=>_entities.EntityAdapters<NayruRescueActorRoomEntity>().Count(actor=>actor.Id==id),
                        0x92=>_entities.EntityAdapters<FallingRockRoomEntity>().Count(),
                        _=>_entities.EntityAdapters<VeranCutsceneWallmasterRoomEntity>().Count()
                    };
                    int native=Enumerable.Range(0,16).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==id);
                    FailIf(port!=native,$"Escape INTERAC ${id:x2} count at update {update}: port={port}, ROM={native}.");
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    !sounds.Requests.SequenceEqual(rom.Sounds)||_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||
                    _entities.ScreenShakeCounter!=rom[0xcd18]||_entities.HorizontalScreenShakeCounter!=rom[0xcd19],
                    $"Escape $1f shared state update {update}: RNG={_random.CaptureState().Calls-seed.Calls}/{rom.RandomCalls}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}, shake={_entities.ScreenShakeCounter},{_entities.HorizontalScreenShakeCounter}/{rom[0xcd18]},{rom[0xcd19]}, SFX={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration)
                {
                    int[] expected=[0x2118,0x2e1f,0x2119,0x210e];
                    FailIf(texts>=expected.Length||rom.Word(0xcba2)!=expected[texts]||!_dialogue.IsOpen,$"Escape dialogue {texts}: unexpected native TX ${rom.Word(0xcba2):x4}.");
                    texts++;generation=rom.TextGeneration;hold=3;
                }
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                sawF3|=_rooms.CurrentRoom.Id==0xf3;
            }
            while(update<1800&&owner.HasState)
            {
                StepGameplayUpdates(owner.State==6?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
                var hand=_entities.EntityAdapters<VeranCutsceneWallmasterRoomEntity>().SingleOrDefault();
                if(batched||hand is null)continue;
                bool capture=drawCheckpoints switch
                {
                    0=>hand.Substate==1&&(hand.ZFixed>>8)>-80&&(hand.ZFixed>>8)<-16,
                    1=>hand.Substate==2&&!_player.Visible,
                    2=>hand.Substate==3&&(hand.ZFixed>>8)<=-8,
                    _=>false
                };
                if(!capture)continue;
                int before=handDraws;
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                FailIf(handDraws<=before||drawnZ!=(hand.ZFixed>>8)||!drawnHand!.IsVisibleInTree(),
                    "The descending, grabbing and rising Wallmaster must actually publish its current height through deferred _Draw.");
                int slot=0xd040+_entities.InteractionSlot(hand.Node)*256;
                CompareInteractionOamRom(rom,drawnHand,slot,$"Wallmaster rendered checkpoint{drawCheckpoints}");
                drawCheckpoints++;
            }
            FailIf(!batched&&drawCheckpoints!=3,"Escape must render the descending hand, Link's grab and the carry-off before the arena warp.");
            FailIf(owner.HasState||texts!=4||!sawF3||!sawRocks||!sawHand,
                "Escape $1f must complete its ordered dialogue, background reload, quake, falling rocks and Wallmaster before requesting the final room.");
            FailIf(rom[0xc2ef]!=1||rom[0xcc47]!=0x85||rom[0xcc48]!=0xd7||rom[0xcc49]!=5||rom[0xcc4a]!=0x77||rom[0xcc4b]!=3||
                _transitions.IsTransitioning||_player.Visible,
                "Escape completion installs $85,$d7,$05,$77,$03 and retains hidden Link through its final object pass.");
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>
            {
                rom.UpdateGameplay(0,0,0xff,++update);rom.ApplyRequestedWarp();
                FailIf(!_transitions.IsTransitioning||!_transitions.PaletteFadeActive||rom[0xc2ef]!=3||
                    _player.Visible!=((rom[0xd01a]&128)!=0)||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    !sounds.Requests.SequenceEqual(rom.Sounds),
                    "The next normal object pass must start CUTSCENE_03's white fade without source walking or an entry sound.");
            });
            bool loaded=false,sawFall=false,sawLanding=false;int arrivalUpdates=0;
            while(arrivalUpdates<240&&!_dialogue.IsOpen)
                StepGameplayUpdates(4,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    rom.AdvanceWarpPalette();
                    if(rom[0xc2ef]==3)rom.UpdateFadeOutWarp();
                    else rom.UpdateGameplay(0,0,0xff,update+1);
                    update++;arrivalUpdates++;
                    rom.AdvanceCameraPosition();
                    loaded|=rom[0xcc2d]==5&&rom[0xcc30]==0xd7;
                    if(loaded)
                    {
                        var face=_entities.EntityAdapters<VeranFaceRoomEntity>().Single();
                        int at=0xd040+_entities.InteractionSlot(face.Node)*256;
                        FailIf(rom[at]==0||rom[at+1]!=0x2d||face.State!=rom[at+4]||face.Node.Visible!=((rom[at+0x1a]&128)!=0),
                            $"Wallmaster arrival update{arrivalUpdates}: Veran's placed $2d interaction state/visibility differs in slot ${at:x4}.");
                    }
                    FailIf(loaded&&_transitions.PaletteFadeActive!=(rom[0xc4ab]!=0),
                        $"Wallmaster arrival palette lifetime update{arrivalUpdates}: port={_transitions.PaletteFadeActive}, ROM mode=${rom[0xc4ab]:x2}, offset=${rom[0xc2ff]:x2}.");
                    FailIf(_rooms.ActiveGroup!=rom[0xcc2d]||_rooms.CurrentRoom.Id!=rom[0xcc30]||
                        _player.Visible!=((rom[0xd01a]&128)!=0)||
                        _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                        !Mathf.IsEqualApprox(_warpFade.Color.A,System.Math.Min((int)rom[0xc2ff],31)/31f),
                        $"Wallmaster arrival update{arrivalUpdates}, batch={batched}: room{_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}/{rom[0xcc2d]:x}:{rom[0xcc30]:x2}, "+
                        $"Link visible={_player.Visible}/{rom[0xd01a]:x2}, XY={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, "+
                        $"fade={_warpFade.Color.A}/{rom[0xc2ff]}, native state={rom[0xd004]:x2}:{rom[0xd005]:x2}.");
                    if(_player.IsRoomWarpFalling)
                    {
                        FailIf(_player.RoomWarpFallZ!=unchecked((sbyte)rom[0xd00f]),
                            $"Wallmaster arrival height update{arrivalUpdates}: port{_player.RoomWarpFallZ}/ROM{unchecked((sbyte)rom[0xd00f])}.");
                        sawFall|=_player.RoomWarpFallZ<0;sawLanding|=_player.RoomWarpFallCollapsed;
                    }
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Wallmaster arrival sounds update{arrivalUpdates}: port={string.Join(',',sounds.Requests)}, ROM={string.Join(',',rom.Sounds)}.");
                });
            FailIf(!loaded||!sawFall||!sawLanding||!_dialogue.IsOpen||rom.Word(0xcba2)!=0x5a13,
                "The Wallmaster warp must load $5:d7, drop Link from above the screen, land and reach Veran's threat.");
            }
            finally
            {
                if(GodotObject.IsInstanceValid(drawnHand))drawnHand!.Draw-=CaptureHandDraw;
                _scene.ProcessMode=ProcessModeEnum.Inherit;
            }
        }
        foreach(int phase in new[]{1,6})
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(4,0xfc,0x80);LoadValidationRoom(4,0xfc);_entities.Clear();
            var owner=_roomEvents.Get<BlackTowerEscapeAttemptEvent>();owner.Begin();
            for(int update=0;update<1500&&owner.State!=phase;update++)
            {
                StepGameplayUpdates(1,Vector2.Zero);
                if(_dialogue.IsOpen)_dialogue.Close();
            }
            FailIf(owner.State!=phase,$"Escape cancellation fixture did not reach native state ${phase:x2}.");
            if(phase==6)StepGameplayUpdates(24,Vector2.Zero,batched:true);
            owner.Cancel();StepGameplayUpdates(4,Vector2.Zero,batched:true);
            FailIf(owner.HasState||_transitions.NativePaletteFadeActive||_entities.PlayerMenusDisabled||!_player.Visible||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||_entities.EntityAdapters<FallingRockRoomEntity>().Any()||
                _entities.EntityAdapters<VeranCutsceneWallmasterRoomEntity>().Any(),
                "Cancelling the exclusive escape scene must retire companions, rock descendants and the hand, and release Link, masks and fade.");
            LoadValidationRoom(4,0xfc);owner.Begin();StepGameplayUpdates(1,Vector2.Zero);
            FailIf(owner.State!=1||_entities.EntityAdapters<NayruRescueActorRoomEntity>().Count()!=2,
                "Re-entering after escape cancellation must initialize exactly two fresh companions.");
            owner.Cancel();
        }
    }
}
