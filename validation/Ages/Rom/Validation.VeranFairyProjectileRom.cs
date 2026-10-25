using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateVeranFairyProjectileRom()
    {
        foreach(int id in new[]{0x2d,0x2f})
        // Levels1/2 are ignored; level3 deflects. Two additional fixtures
        // cover parent death with a free/full interaction pool.
        for(int fixture=1;fixture<=5;fixture++)
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(4,0xfc,0x80);LoadValidationRoom(4,0xfc);_entities.Clear();
            int level=fixture<=3?fixture:0;
            if(level!=0)_inventory.GiveTreasure(TreasureId.Shield,level);
            _inventory.EquipA(level==0?0:TreasureId.Shield);_inventory.EquipB(0);
            _player.WarpTo(level==0?new(24,152):new(120,100));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Fairy projectile fixture must start on the real arena floor.");
            var fairy=_roomEvents.Get<BlackTowerPossessionEvent>().SpawnFairy(new(120,64))!;var parent=fairy.Actor;
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,(int)_player.Position.X,(int)_player.Position.Y)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd4;rom[0xcc0a]=0xd5;rom[0xcc0c]=0xd6;rom[0xcc0e]=0x8e;
            rom[0xd080]=1;rom[0xd081]=6;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            VeranFairyProjectileRoomEntity? shot=null;int at=0,update=0,pressed=level==0?0:1;
            var sounds=_sound.AttachPlayRequestAudit();bool contact=false;
            void Compare()
            {
                rom.UpdateGameplay(pressed,level==0?0:1,255,++update);pressed=0;
                if(shot is not null)
                {
                    FailIf(shot.Finished!=(rom[at]==0)||!shot.Finished&&(shot.State!=rom[at+4]||shot.Counter!=rom[at+6]||shot.Angle!=rom[at+9]||
                        shot.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)),
                        $"Fairy PART${id:x2} fixture{fixture} update{update}: port{shot.Finished}/{shot.State}/{shot.Counter}/{shot.Position}, ROM{rom[at]}/{rom[at+4]}/{rom[at+6]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}.");
                    contact|=shot.Finished;
                }
                var random=_random.CaptureState();
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||_player.HealthQuarters!=rom[0xc6aa]||
                    _player.ActiveTransformation!=(rom[0xd001]==2?2:0)||random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||
                    random.Calls-seed.Calls!=rom.RandomCalls||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Fairy PART${id:x2} fixture{fixture} update{update}: Link/transform/RNG/sounds differ ({string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}).");
                FailIf(_entities.Entities<PuzzlePuffEffect>().Count!=Enumerable.Range(2,14).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==5),"Fairy projectile parent-death puff allocation differs.");
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,level==0?[]:["attack"],[],batched:fixture%2==0,afterUpdate:Compare);
            StepGameplayUpdates(1,Vector2.Zero,level==0?[]:["attack"],level==0?[]:["attack"],afterUpdate:Compare);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,4);rom[AgesWramAddress.wDisabledObjects]=4;
            _runtimeState.SetWramByte(AgesWramAddress.wMenuDisabled,0);rom[AgesWramAddress.wMenuDisabled]=0;
            shot=new(_entities,parent.Data,parent,id,new(120,84),0,16);_entities.AddEntity(shot);
            int partSlot=Enumerable.Range(0,16).Single(slot=>ReferenceEquals(_entities.ResolvePartSlot(slot),shot));at=0xd0c0+partSlot*256;
            rom[at]=1;rom[at+1]=(byte)id;rom[at+11]=84;rom[at+13]=120;rom[at+9]=16;rom.Word(at+0x16,0xd080);
            if(level==0)
            {
                Step(2);
                // All physical item columns are masked off for these parts.
                // Execute the ROM collision walk independently of imported bits.
                byte parentCollision=rom[0xd0a4];rom[0xd0a4]&=0x7f;
                for(int collision=4;collision<32;collision++)
                {
                    rom[0xd724]=(byte)(0x80|collision);rom[0xd726]=rom[0xd727]=6;rom[0xd728]=0xff;
                    rom[0xd70b]=(byte)shot.Position.Y;rom[0xd70d]=(byte)shot.Position.X;
                    rom.AdvanceEnemyAndPartCollisions();
                    FailIf(rom[at+0x2a]!=0||shot.ApplyItemCollision((RoomEntityItemCollision)collision,shot.CollisionBounds,shot.Position,1,new List<RoomEntitySpawn>()),"Fairy projectile accepted a source-masked physical item.");
                }
                rom[0xd724]=0;rom[0xd0a4]=parentCollision;
                var interactionSlots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots");
                var reservations=new List<EnemyAiSlotReservation>();
                if(fixture==5)
                    for(int slot=2;slot<16;slot++)
                    {
                        var reservation=new EnemyAiSlotReservation(fairy.Node);interactionSlots.Add(reservation,slot);reservations.Add(reservation);
                        int address=0xd040+slot*256;rom[address]=1;rom[address+1]=0x33;rom[address+4]=1; // Source state1 waits for enemy count==1.
                    }
                if(fixture==5)rom[0xcdd1]=2; // Keep the source reservation handlers waiting.
                parent.Health=0;rom[0xd0a9]=0;Step(1);
                FailIf(!shot.Finished||_entities.Entities<PuzzlePuffEffect>().Count!=(fixture==4?1:0),"Parent death must delete its projectile and make one checked puff allocation.");
                foreach(var reservation in reservations)
                {
                    int address=0xd040+interactionSlots[reservation]*256;interactionSlots.Remove(reservation);
                    for(int offset=0;offset<64;offset++)rom[address+offset]=0;
                }
                if(fixture==5){rom[0xcdd1]=1;Step(2);FailIf(_entities.Entities<PuzzlePuffEffect>().Count!=0,"Releasing the interaction pool must not retry a deleted projectile's puff.");}
            }
            else
            {
                for(int bound=0;bound<100&&!contact;bound++)Step(1);
                FailIf(!contact,"Fairy projectile never reached the physical shield/body fixture.");
                Step(3);
                FailIf(level==3?_player.ActiveTransformation!=0||_player.HealthQuarters!=_inventory.MaxHealthQuarters:
                    id==0x2f?_player.ActiveTransformation!=2:_player.HealthQuarters!=_inventory.MaxHealthQuarters-4,
                    "Fairy projectile must preserve shield3 deflection, weaker-shield contact damage and baby-Link handoff.");
            }
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranFairyProjectileRoomEntity>().Any()||_entities.EntityAdapters<VeranFairyRoomEntity>().Any(),"Fairy projectile cancellation retained its parent or PART.");
        }
    }
}
