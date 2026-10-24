using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaMergeRom()
    {
        var animationCounter = typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (bool batched in new[] { false,true })
        foreach (int parentSlot in new[] { 0,5 })
        foreach (bool reserveLower in parentSlot==5 ? new[] { false,true } : new[] { false })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0xf5);
            var placed = _entities.EntityAdapters<TwinrovaRoomEntity>().Single(); var world = placed.Actor.World;
            _entities.RetireEntity(placed);
            var boss = new TwinrovaRoomEntity(new(world,Vector2.Zero,0,0x80));
            _entities.RegisterEnemySlot(boss,parentSlot); _entities.AddEntity(boss);
            var blockers = reserveLower ? new EnemyAiSlotReservation[5] : [];
            for(int slot=0;slot<blockers.Length;slot++)
            { blockers[slot]=new(); _entities.RegisterEnemySlot(blockers[slot],slot); _entities.AddEntity(blockers[slot]); }
            _player.WarpTo(new(0x78,0x78)); _player.Face(Vector2I.Up); _transitions.ResetCamera();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78)
            { HostileEnemiesEnabled = true, HostilePartsEnabled = true, PostObjectCollisionsEnabled = false };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd080+parentSlot*256]=1;rom[0xd081+parentSlot*256]=3;rom[0xd083+parentSlot*256]=0x80;
            for(int slot=0;slot<blockers.Length;slot++)
            {
                int at=0xd080+slot*256;
                rom[at]=1;rom[at+1]=3;rom[at+4]=2;rom[at+0x29]=1;
            }
            rom[0xcc08]=0x33;rom[0xcc0a]=0x34;rom[0xcc0c]=0x35;rom[0xcc0e]=0x36;
            rom[0xcdd1]=1;
            rom[0xcd17]=0xd0; // resetCamera's focus on Link (type $00).
            Vector2 camera = -_transitions.WorldToGameplayScreen(Vector2.Zero);
            rom[0xffac]=(byte)camera.X;rom[0xffaa]=(byte)camera.Y;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, generation = 0, hold = 0;
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(2);
            for(int slot=0;slot<blockers.Length;slot++)
            {
                _entities.RetireEntity(blockers[slot]);
                for(int offset=0;offset<64;offset++) rom[0xd080+slot*256+offset]=0;
            }
            var hit = _entities.EntityAdapters<TwinrovaRoomEntity>().Single(t=>t.Actor.Subid==0);
            hit.Actor.Health = 0;hit.Actor.Flags |= 0x40;
            rom[0xd0a9+hit.Slot*256]=0;rom[0xd0b2+hit.Slot*256]|=0x40;
            // The reflected critical hit was independently verified in the
            // projectile regression. This fixture begins at its shared signal.
            Step(200);
            while (!_entities.EntityAdapters<MergedTwinrovaRoomEntity>().Any() && update < 600) Step(1);
            var merged = _entities.EntityAdapters<MergedTwinrovaRoomEntity>().SingleOrDefault();
            FailIf(merged is null || update>=600 || _entities.EntityAdapters<TwinrovaRoomEntity>().Any(),
                "Native merge did not replace the split pair with ENEMY $01 within its bounded animation/fade handoff.");
            FailIf(merged!.Actor.Slot!=(reserveLower ? 6 : 1),
                "twinrova_state10@substate7 must scan only above its own slot, even with earlier slots free.");
            Step(10); // Source intro text, then eight-update delay; no arena playthrough.
            FailIf(merged!.Actor.State!=9 || merged.Actor.BossHealth!=5 || _entities.RoomEnemyCount!=1,
                "Merged Twinrova intro must release Link with five seed hits, state$09 and the original room enemy count.");
            _sound.SetRequestObserver(null);

            void Compare()
            {
                rom.AdvanceWarpPalette(); rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                for(int slot=0;slot<16;slot++)
                {
                    int at=0xd080+slot*256;
                    if(_entities.ResolveEnemySlot(slot) is EnemyAiSlotReservation) continue;
                    var twin=_entities.ResolveEnemySlot(slot) as TwinrovaRoomEntity;
                    FailIf((twin is not null)!=(rom[at]!=0&&rom[at+1]==3),$"Merge update{update}: split ENEMY slot${slot:x2} occupancy differs.");
                    if(twin is not null)
                    {
                        var a=twin.Actor;
                        FailIf(a.State!=rom[at+4] || a.Substate!=rom[at+5] || a.Counter!=rom[at+6] || a.Flags!=rom[at+0x32] ||
                            a.Angle!=rom[at+9] || a.Direction!=rom[at+8] || a.FacingDelay!=rom[at+0x37] ||
                            a.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f) || a.ZFixed!=unchecked((short)rom.Word(at+14)) ||
                            a.Visible!=((rom[at+0x1a]&128)!=0) || (int)animationCounter.GetValue(a.Animation)! !=rom[at+0x20],
                            $"Merge update{update},slot${slot:x2}: state={a.State:x2}:{a.Substate:x2}/{rom[at+4]:x2}:{rom[at+5]:x2}, count={a.Counter}/{rom[at+6]},flags={a.Flags:x2}/{rom[at+0x32]:x2},pos={a.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f},Z={a.ZFixed}/{unchecked((short)rom.Word(at+14))},anim={animationCounter.GetValue(a.Animation)}/{rom[at+0x20]},cameraY={world.CameraY(a.Position)}/{rom[0xffaa]}.");
                    }
                    var next=_entities.ResolveEnemySlot(slot) as MergedTwinrovaRoomEntity;
                    FailIf((next is not null)!=(rom[at]!=0&&rom[at+1]==1),$"Merge update{update}: merged ENEMY slot${slot:x2} occupancy differs.");
                    if(next is not null)
                    {
                        var a=next.Actor;
                        FailIf(a.State!=rom[at+4] || a.Subid!=rom[at+2] || a.Counter!=rom[at+6] || a.SwapCounter!=rom[at+0x30] ||
                            a.BossHealth!=rom[at+0x34] || a.Health!=rom[at+0x29] || a.Palette!=rom[at+0x1b] || a.CollisionType!=rom[at+0x24] ||
                            a.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f) || a.ZFixed!=unchecked((short)rom.Word(at+14)) ||
                            (int)animationCounter.GetValue(a.Animation)! !=rom[at+0x20],
                            $"Merge update{update},slot${slot:x2}: fresh ENEMY01 state={a.State}/{rom[at+4]},count={a.Counter}/{rom[at+6]},swap={a.SwapCounter}/{rom[at+0x30]},HP={a.Health}/{rom[at+0x29]},palette={a.Palette}/{rom[at+0x1b]},collision={a.CollisionType:x2}/{rom[at+0x24]:x2},Z={a.ZFixed}/{unchecked((short)rom.Word(at+14))}.");
                    }
                }
                FailIf(_entities.RoomEnemyCount!=rom[0xcdd1] || RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls) ||
                    !sounds.Requests.SequenceEqual(rom.Sounds) || _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0) ||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=rom[0xcc8a] || _runtimeState.ReadWramByte(0xcbca)!=rom[0xcbca],
                    $"Merge update{update}: count={_entities.RoomEnemyCount}/{rom[0xcdd1]},RNG={_random.Calls-seed.Calls}/{rom.RandomCalls},sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)},fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}.");
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=3;FailIf(!_dialogue.IsOpen,"Merge native text did not open its runtime dialogue.");}
                if(hold!=0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                _player.ApplyInteractionInvincibility(128);rom[0xd02b]=128;
            }
        }
        GD.Print("Validated Twinrova merge ascent/circle/fade, native slot replacement/count and merged boss intro in split/batched gameplay against the clean ROM.");
    }
}
