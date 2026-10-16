using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranBeeAllocationRom()
    {
        foreach(bool partsFull in new[]{true,false})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);
            var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();var actor=boss.Actor;
            foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,136));_transitions.ResetCamera();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,136)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=0xd7;rom[0xcc0a]=0xd8;rom[0xcc0c]=0xd9;rom[0xcc0e]=0xda;
            rom[0xd080]=1;rom[0xd081]=2;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            int update=0;var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom.UpdateGameplay(0,0,255,++update);
                FailIf(actor.State!=rom[0xd084]||actor.Counter!=rom[0xd086]||actor.Counter2!=rom[0xd087]||
                    actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f),"Bee allocation failure must retain native counter/movement order.");
                foreach(var shot in _entities.EntityAdapters<VeranBeeProjectileRoomEntity>())
                {
                    int slot=Enumerable.Range(0,16).Single(index=>ReferenceEquals(_entities.ResolvePartSlot(index),shot)),at=0xd0c0+slot*256;
                    FailIf(rom[at+1]!=0x58||shot.State!=rom[at+4]||shot.Counter!=rom[at+6],$"Bee projectile update{update} slot{slot}: port{shot.State}/{shot.Counter}, ROM id${rom[at+1]:x2}/{rom[at+4]}/{rom[at+6]}, live {string.Join(',',Enumerable.Range(0,16).Where(index=>rom[0xd0c0+index*256]!=0).Select(index=>$"{index}:{rom[0xd0c1+index*256]:x2}"))}.");
                }
                var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
                foreach(var child in _entities.EntityAdapters<VeranChildBeeRoomEntity>())
                {
                    int at=0xd080+slots[child]*256;
                    FailIf(rom[at+1]!=0x1f||child.Actor.State!=rom[at+4]||child.Actor.Counter!=rom[at+6]||child.Actor.SubId!=rom[at+2],"Bee child must initialize in the same ordered enemy pass as its allocation.");
                }
                var random=_random.CaptureState();
                FailIf(random.Calls-seed.Calls!=rom.RandomCalls||random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||
                    !sounds.Requests.SequenceEqual(rom.Sounds)||_entities.RoomEnemyCount!=rom[0xcdd1],"Bee allocation success/failure changed source RNG, sounds or enemy count.");
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:!partsFull,afterUpdate:Compare);
            Step(1);
            void Set(string property,object value)=>typeof(VeranFinalCharacter).GetProperty(property,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,value);
            Set("SubId",2);Set("CollisionMode",0x6b);Set("State",partsFull?6:10);Set("Counter",partsFull?1:25);Set("Counter2",2);Set("Speed",0x50);Set("Angle",8);Set("Target",new Vector2I(232,64));
            rom[0xd082]=2;rom[0xd0a5]=0x6b;rom[0xd084]=(byte)actor.State;rom[0xd086]=(byte)actor.Counter;rom[0xd087]=2;rom[0xd090]=0x50;rom[0xd089]=8;rom[0xd0b6]=64;rom[0xd0b7]=232;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);rom[WramAddress.wDisabledObjects]=0;
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled,0);rom[WramAddress.wMenuDisabled]=0;
            var reservations=new List<(EnemyAiSlotReservation Owner,int Slot)>();
            var parts=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            var enemies=SomariaPrivate<HashSet<int>>(_entities,"_reservedEnemySlots");
            for(int slot=0;slot<16;slot++)
            {
                if(partsFull)
                {
                    if(_entities.ResolvePartSlot(slot) is not null)continue;
                    var owner=new EnemyAiSlotReservation(boss.Node);parts.Add(owner,slot);reservations.Add((owner,slot));
                    int at=0xd0c0+slot*256;rom[at]=1;rom[at+1]=0x0a;rom[at+4]=1; // Native nil PART.
                }
                else if(slot!=0)
                {
                    enemies.Add(slot);reservations.Add((new(boss.Node),slot));
                    int at=0xd080+slot*256;rom[at]=1;rom[at+1]=0x1f;rom[at+4]=1;rom[at+0x29]=1; // Child-bee stub, no RNG or count ownership.
                }
            }
            Step(1);
            FailIf(actor.Counter!=(partsFull?15:24)||_entities.EntityAdapters<VeranBeeProjectileRoomEntity>().Any()||_entities.EntityAdapters<VeranChildBeeRoomEntity>().Any(),"A full pool must consume its fire boundary without allocating.");
            foreach(var reservation in reservations)
            {
                if(partsFull)parts.Remove(reservation.Owner);else enemies.Remove(reservation.Slot);
                // Native deletion clears the whole object, not just enabled.
                int at=(partsFull?0xd0c0:0xd080)+reservation.Slot*256;
                for(int offset=0;offset<(partsFull?64:128);offset++)rom[at+offset]=0;
            }
            Step(partsFull?14:7);
            FailIf(_entities.EntityAdapters<VeranBeeProjectileRoomEntity>().Any()||_entities.EntityAdapters<VeranChildBeeRoomEntity>().Any(),"Freeing the pool must not replay the failed fire request.");
            Step(1);
            FailIf(partsFull?_entities.EntityAdapters<VeranBeeProjectileRoomEntity>().Count()!=1:_entities.EntityAdapters<VeranChildBeeRoomEntity>().Single().Actor.SubId!=1,"The next native fire boundary must allocate exactly its next source object.");
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranBeeProjectileRoomEntity>().Any()||_entities.EntityAdapters<VeranChildBeeRoomEntity>().Any(),"Room replacement must retire both bee allocation paths.");
        }
    }
}
