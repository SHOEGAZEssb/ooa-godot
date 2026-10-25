using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVeranBeeShieldRom()
    {
        foreach(int level in new[]{1,2,3})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.GiveTreasure(TreasureId.Shield,level);_inventory.EquipA(TreasureId.Shield);_inventory.EquipB(0);
            _player.WarpTo(new(120,100));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Bee shield fixture must use the actual arena floor.");
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,100)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,pressed=1,at=0;bool collided=false;
            VeranBeeProjectileRoomEntity? shot=null;
            void Compare()
            {
                rom.UpdateGameplay(pressed,1,255,++update);pressed=0;
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||_player.HealthQuarters!=rom[0xc6aa]||
                    _entities.PlayerSwordDisabled!=((rom[0xccd8]&32)!=0)||_entities.PlayerMovementDisabled!=((rom[0xcc61]&32)!=0)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Bee shield L{level} update{update}: Link/restrictions/sounds differ ({string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}).");
                if(shot is not null)
                {
                    FailIf(shot.Finished!=(rom[at]==0)||!shot.Finished&&(shot.State!=rom[at+4]||shot.Counter!=rom[at+6]||
                        (shot.InvincibilityCounter&255)!=rom[at+0x2b]||shot.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)),"Bee shield PART status/position differs.");
                    collided|=shot.State==3||shot.InvincibilityCounter!=0;
                }
                FailIf(_random.CaptureState().Calls!=seed.Calls+rom.RandomCalls,"Bee shield must preserve shared RNG consumption.");
            }
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:Compare);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,4);rom[AgesWramAddress.wDisabledObjects]=4;
            _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled,0);rom[AgesWramAddress.wMenuDisabled]=0;
            shot=new(_entities,boss.Actor.Data,boss.Slot,boss.Actor.Position,0);_entities.AddEntity(shot);
            int slot=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots")[shot];at=0xd0c0+slot*256;
            rom[at]=1;rom[at+1]=0x58;rom[at+11]=64;rom[at+13]=120;rom.Word(at+0x16,0xd080);
            for(int count=0;count<24&&!collided;count++)StepGameplayUpdates(1,Vector2.Zero,["attack"],afterUpdate:Compare);
            FailIf(!collided||(level==1)!=(shot.State==3)||_player.HealthQuarters!=(level==1?_inventory.MaxHealthQuarters-4:_inventory.MaxHealthQuarters),
                "Shield1 must leave PART$58 body contact enabled; shields2/3 deflect without damage or attachment.");
            StepGameplayUpdates(level==3?2:12,Vector2.Zero,["attack"],batched:level==2,afterUpdate:Compare);
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranBeeProjectileRoomEntity>().Any()||_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,"Room cancellation must release attached/deflected bee projectile ownership.");
        }
    }
    private void ValidateVeranBeeContactRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool mash in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,84));_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Bee contact must begin on the arena's actual floor below the source parent.");
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,84)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            int update=0,pressed=0,held=0,angle=255;VeranBeeProjectileRoomEntity? shot=null;int at=0;
            var sounds=_sound.AttachPlayRequestAudit();bool sawCaught=false,sawMove=false,sawStop=false;
            void Compare()
            {
                rom.UpdateGameplay(pressed,held,angle,++update);rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    _player.HealthQuarters!=rom[0xc6aa]||_entities.PlayerSwordDisabled!=((rom[0xccd8]&32)!=0)||
                    _entities.PlayerMovementDisabled!=((rom[0xcc61]&32)!=0)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Bee $58 contact update {update}, batch={batched}, mash={mash}: Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, HP={_player.HealthQuarters}/{rom[0xc6aa]}, sword/move={_entities.PlayerSwordDisabled}/{_entities.PlayerMovementDisabled}, native={rom[0xccd8]:x2}/{rom[0xcc61]:x2}, SFX={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(shot is not null)
                {
                    FailIf(shot.Finished!=(rom[at]==0)||!shot.Finished&&(shot.State!=rom[at+4]||shot.Counter!=rom[at+6]||
                        shot.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||shot.Visible!=((rom[at+0x1a]&128)!=0)),
                        $"Bee attachment update {update}: port={shot.State}/{shot.Counter}/{shot.Position}, ROM={rom[at+4]}/{rom[at+6]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}.");
                    sawCaught|=shot.State==3;sawStop|=_entities.PlayerMovementDisabled;sawMove|=sawCaught&&!_entities.PlayerMovementDisabled;
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    "Bee attachment must preserve shared gameplay RNG.");pressed=0;
            }
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            // Isolate the projectile's Link handoff from the parent's attack AI.
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,4);rom[AgesWramAddress.wDisabledObjects]=4;
            _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled,0);rom[AgesWramAddress.wMenuDisabled]=0;
            var data=boss.Actor.Data;shot=new(_entities,data,boss.Slot,boss.Actor.Position,0);_entities.AddEntity(shot);
            int slot=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots")[shot];at=0xd0c0+slot*256;
            rom[at]=1;rom[at+1]=0x58;rom[at+11]=64;rom[at+13]=120;rom.Word(at+0x16,0xd080);
            StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(shot.State!=3||shot.Counter!=239,"Body contact must enter the source 240-update restriction and decrement on that same dispatch.");
            held=0x10;angle=8;
            for(int bound=0;bound<260&&!shot.Finished;bound+=4)
            {
                if(mash&&bound%16==0)pressed=0x10;
                StepGameplayUpdates(4,Vector2.Right,MenuRomActions(held),MenuRomActions(pressed),batched,Compare);
            }
            FailIf(!shot.Finished||!sawCaught||!sawMove||!sawStop||_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,
                "The attached projectile must alternate movement, suppress swords, respond to fresh keys and release every restriction on deletion.");
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranBeeProjectileRoomEntity>().Any()||_entities.PlayerSwordDisabled||_entities.PlayerMovementDisabled,
                "Replacing the room must retire any projectile restriction owner.");
        }
    }
}
