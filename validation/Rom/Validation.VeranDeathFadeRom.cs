using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranDeathFadeRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(var fixture in new[]{(Handoff:false,Maku:15),(Handoff:true,Maku:15),(Handoff:true,Maku:16)})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);
            _saveData.SetMakuTreeState(fixture.Maku);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();var actor=boss.Actor;
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(e=>e!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,136));_transitions.ResetCamera();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,136)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,explosions=0;bool bossPresent=true;
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,++update);rom.SelectTriggeredCutscene();
                FailIf(_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0),
                    $"Veran $90:$21 palette lifetime differs at update{update}, batch={batched}.");
                if(bossPresent&&rom[0xd080]!=0)FailIf(actor.Substate!=rom[0xd085]||actor.Counter2!=rom[0xd087],
                    $"Veran's fade allocation/counter handoff differs at update{update}: {actor.Substate}/{actor.Counter2} vs{rom[0xd085]}/{rom[0xd087]}.");
                foreach(var control in _entities.EntityAdapters<VeranDeathFadeRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(control.Node)*256;
                    FailIf(rom[at+1]!=0x90||rom[at+2]!=0x21||control.State!=rom[at+4]||control.Node.Position!=new Vector2(rom[at+13],rom[at+11]),
                        $"Veran death fade interaction slot ${at:x4} does not match source at update{update}: port={control.State}/{control.Node.Position}, ROM=${rom[at]:x2}:${rom[at+1]:x2}:${rom[at+2]:x2}/{rom[at+4]}/{rom[at+13]},{rom[at+11]}.");
                }
                foreach(var explosion in _entities.Entities<InteractionExplosionEffect>())
                {
                    int at=0xd040+_entities.InteractionSlot(explosion)*256;explosions++;
                    FailIf(rom[at+1]!=0x56||rom[at+3]!=0||explosion.Position!=new Vector2(rom[at+13],rom[at+11])||
                        explosion.AnimationParameter!=rom[at+0x21]||explosion.ZIndex!=ObjectDrawPriority.BehindLinkZIndex,
                        $"Veran fade explosion ${at:x4} update{update}: position/animation/draw priority must follow INTERAC $56/v00.");
                }
                int port=_entities.EntityAdapters<VeranDeathFadeRoomEntity>().Count()+_entities.Entities<InteractionExplosionEffect>().Count;
                int native=Enumerable.Range(0,16).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256] is 0x90 or 0x56);
                var random=_random.CaptureState();FailIf(port!=native||!sounds.Requests.SequenceEqual(rom.Sounds)||
                    random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||random.Calls-seed.Calls!=rom.RandomCalls,
                    $"Veran fade source slot count/SFX/RNG mismatch at update{update}: slots={port}/{native}, sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            actor.Health=1;rom[0xd0a9]=1;
            foreach(var value in new[]{("State",10),("Substate",2),("Counter2",1)})
                typeof(VeranFinalCharacter).GetProperty(value.Item1,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,value.Item2);
            rom[0xd084]=10;rom[0xd085]=2;rom[0xd087]=1;
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(actor.Substate!=3||_entities.EntityAdapters<VeranDeathFadeRoomEntity>().Single().State!=1||!_transitions.NativePaletteFadeActive,
                "Boss allocation must create $90:$21, initialize it in the later interaction pass and start delay4 white fade that same update.");
            StepGameplayUpdates(120,Vector2.Zero,batched:batched,afterUpdate:Compare);
            if(fixture.Handoff)
            {
                var ending=_roomEvents.Get<BlackTowerEndingEvent>();
                for(int wait=0;wait<20&&!ending.HasState;wait++)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
                FailIf(!ending.HasState||rom[0xc2ef]!=9||rom[0xd080]!=0||!actor.IsDead||
                    !_saveData.HasRoomFlag(4,0xfc,0x80)||(rom[0xc9fc]&0x80)==0||
                    _saveData.MakuTreeState!=16||rom[0xc6e8]!=16||_entities.RoomEnemyCount!=rom[0xcdd1],
                    "The native fade completion must delete Veran, set $4:fc flag80, saturate Maku state$10 and request CUTSCENE$09 without decrementing the room count.");
                FailIf(_rooms.ActiveGroup!=4||_rooms.CurrentRoom.Id!=0xd4,"The enemy's ending trigger must defer the new room until the next native cutscene dispatch.");
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>
                {
                    rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateGameplay(0,0,255,++update);
                    FailIf(_rooms.ActiveGroup!=1||_rooms.CurrentRoom.Id!=0x76||rom[0xcc2d]!=1||rom[0xcc30]!=0x76||
                        ending.State!=1||_entities.EntityAdapters<NayruRescueActorRoomEntity>().Count()!=2,
                        "The first CUTSCENE$09 dispatch must load $1:76 and its two imported companions after Veran's final object pass.");
                });
                ending.Cancel();LoadValidationRoom(4,0xd5);continue;
            }
            // Isolate the controller's terminal deletion from CUTSCENE_09,
            // whose own gameplay handoff is covered separately.
            _entities.RetireEntity(_entities.ResolvePartSlot(0)!);rom[0xd0c0]=0;
            _entities.RetireEntity(boss);rom[0xd080]=0;bossPresent=false;
            StepGameplayUpdates(48,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(_transitions.NativePaletteFadeActive||_entities.EntityAdapters<VeranDeathFadeRoomEntity>().Any()||
                _entities.Entities<InteractionExplosionEffect>().Any()||explosions==0,
                "The palette's terminal update must delete $90:$21 and allow all source explosions to finish.");
            LoadValidationRoom(4,0xd5);
            FailIf(_transitions.NativePaletteFadeActive||_entities.PlayerMenusDisabled,
                "Room replacement must release the existing fade/input owner.");
        }
    }
}
