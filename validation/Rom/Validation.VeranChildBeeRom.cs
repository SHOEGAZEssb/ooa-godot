using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranChildBeeGameplayRom()
    {
        foreach(int item in new[]{TreasureId.SwitchHook,TreasureId.Shovel,TreasureId.CaneOfSomaria,ItemId.EmberSeed,ItemId.ScentSeed,ItemId.PegasusSeed,ItemId.GaleSeed,ItemId.MysterySeed})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);_entities.Clear();
            bool hook=item==TreasureId.SwitchHook,shovel=item==TreasureId.Shovel,somaria=item==TreasureId.CaneOfSomaria,shooter=!hook&&!shovel&&!somaria;
            _inventory.GiveTreasure(shooter?TreasureId.Shooter:item,1);
            if(shooter){_inventory.GiveTreasure(item,20);_inventory.SelectShooterSeeds(item-0x20);}
            _inventory.EquipA(shooter?TreasureId.Shooter:item);_inventory.EquipB(0);
            _player.WarpTo(new(120,112));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Child-bee attack must approach from the actual arena floor.");
            int targetY=shovel||somaria?96:80;
            var data=new VeranFinalDatabase();var actor=new VeranChildBeeCharacter(_entities,data,0,new(120,targetY),_currentRoom);
            var bee=new VeranChildBeeRoomEntity(actor,_entities,data);_entities.AddEntity(bee);
            var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,0,120,112)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xd080]=1;rom[0xd081]=0x1f;rom[0xd08b]=(byte)targetY;rom[0xd08d]=120;rom[0xcdd1]=1;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,pressed=0,held=0;bool hit=false;
            void Compare()
            {
                rom.UpdateGameplay(pressed,held,255,++update);pressed=0;
                bool dead=rom[0xd080]==0;
                FailIf(bee.Finished!=dead||!dead&&(actor.Health!=rom[0xd0a9]||(actor.InvincibilityCounter&255)!=rom[0xd0ab]||
                    actor.StunCounter!=rom[0xd0ae]||actor.KnockbackCounter!=rom[0xd0ad]||actor.State!=rom[0xd084]||
                    actor.Position!=(bee.GaleCaught?new Vector2(rom[0xd08d],rom[0xd08b]):new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f))),
                    $"Child bee real item${item:x2} update{update}: native target state/status/position differs.");
                hit|=actor.NativeHitPending||actor.StunCounter!=0||bee.GaleCaught||bee.Finished;
                var random=_random.CaptureState();
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    _entities.RoomEnemyCount!=rom[0xcdd1]||random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||
                    random.Calls-initial.Calls!=rom.RandomCalls||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Child bee real item${item:x2} update{update}: Link/RNG/count/sound differs ({string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}).");
            }
            void Step(int count,int keys=0){pressed=keys;held=keys;StepGameplayUpdates(count,Vector2.Zero,MenuRomActions(keys),MenuRomActions(keys),batched:!hook,afterUpdate:Compare);}
            Step(1);
            typeof(VeranChildBeeCharacter).GetProperty("State",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,9);
            typeof(VeranChildBeeCharacter).GetProperty("Counter",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,200);rom[0xd084]=9;rom[0xd086]=200;
            Step(1,1);if(shooter)Step(5,1);Step(60);
            FailIf(!hit||_player.IsUsingSwitchHook||_entities.SwitchHook!.ExchangeActive,"Real hook/seed flight must hit $1f and finish its parent without exchanging Link.");
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranChildBeeRoomEntity>().Any()||_entities.EntityAdapters<BurningEnemyRoomEntity>().Any(),"Cancelling live child-bee burn/Gale/stun must release its actors.");
        }
    }
    private void ValidateVeranChildBeeItemsRom()
    {
        int fixture=0;
        var cases=new[]{4,5,6,7,8,9,11,12,13,0x16,0x17,0x18,0x19,0x1a,0x1b,0x1c,0x1d,0x1e}
            .Select(collision=>(Collision:collision,Health:collision==0x1b?4:1,Full:false)).ToList();
        cases.Add((0x1b,1,false));cases.Add((0x1b,1,true));
        foreach(var spec in cases)
        {
            int collision=spec.Collision;
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,136));_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Child bee fixture requires the real arena floor.");
            var data=new VeranFinalDatabase();var actor=new VeranChildBeeCharacter(_entities,data,0,new(120,64),_currentRoom);
            var bee=new VeranChildBeeRoomEntity(actor,_entities,data);_entities.AddEntity(bee);
            var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,0,120,136)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xd080]=1;rom[0xd081]=0x1f;rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;bool batched=(fixture++&1)!=0;
            void Compare()
            {
                rom.UpdateGameplay(0,0,255,++update);
                bool dead=rom[0xd080]==0;
                FailIf(bee.Finished!=dead||!dead&&(actor.Health!=rom[0xd0a9]||(actor.InvincibilityCounter&255)!=rom[0xd0ab]||
                    actor.StunCounter!=rom[0xd0ae]||actor.KnockbackCounter!=rom[0xd0ad]||actor.State!=rom[0xd084]||
                    actor.Position!=(bee.GaleCaught?new Vector2(rom[0xd08d],rom[0xd08b]):new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f))||
                    (bee.GaleCaught?actor.GaleDrawZ:actor.BurnZFixed>>8)!=unchecked((sbyte)rom[0xd08f])),
                    $"Child bee column${collision:x2} update{update}: port dead/HP/inv/stun/recoil/state/XY/Z={bee.Finished}/{actor.Health}/{actor.InvincibilityCounter}/{actor.StunCounter}/{actor.KnockbackCounter}/{actor.State}/{actor.Position}/{actor.BurnZFixed>>8}; ROM={dead}/{rom[0xd0a9]}/{rom[0xd0ab]}/{rom[0xd0ae]}/{rom[0xd0ad]}/{rom[0xd084]}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f}/{unchecked((sbyte)rom[0xd08f])}.");
                FailIf(RandomDiffers(initial, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    _entities.RoomEnemyCount!=rom[0xcdd1]||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Child bee column${collision:x2} update{update}: RNG/count/sounds differ: {_random.CaptureState().Calls-initial.Calls}/{rom.RandomCalls}, {_entities.RoomEnemyCount}/{rom[0xcdd1]}, {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(1);
            // Declare a long state9 wait to isolate common status from flight.
            // The ROM executes the same normal handler and all object phases.
            typeof(VeranChildBeeCharacter).GetProperty("State",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,9);
            typeof(VeranChildBeeCharacter).GetProperty("Counter",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,200);
            actor.Health=spec.Health;rom[0xd084]=9;rom[0xd086]=200;rom[0xd0a9]=(byte)actor.Health;
            var reservations=new List<EnemyAiSlotReservation>();
            if(spec.Full)
            {
                var parts=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
                for(int slot=0;slot<16;slot++)
                {
                    var reservation=new EnemyAiSlotReservation();
                    parts.Add(reservation,slot);
                    _entities.AddEntity(reservation);
                    reservations.Add(reservation);
                    int at=0xd0c0+slot*256;rom[at]=1;rom[at+1]=0x0a;rom[at+4]=1; // Native nil PART, no updates or RNG.
                }
            }
            var spawns=new List<RoomEntitySpawn>();
            if(collision<0x1a)
            {
                int damage=collision is 5 or 8?2:collision is 6 or 7 or 11 or 0x18?4:1;
                rom.HitEnemyWithItem(0,collision,damage);
                FailIf(!bee.ApplyItemCollision((RoomEntityItemCollision)collision,actor.CollisionBounds,actor.Position-Vector2.Right,damage,spawns),"Child bee rejected an eligible native weapon.");
            }
            else
            {
                int item=collision switch {0x1a=>ItemId.MysterySeed,0x1b=>ItemId.EmberSeed,0x1c=>ItemId.ScentSeed,0x1d=>ItemId.PegasusSeed,_=>ItemId.GaleSeed};
                FailIf(!new SeedSatchelDatabase().TryGet(item,out var seed),"Missing child-bee seed fixture.");
                rom.HitEnemyWithItem(0,collision,-(sbyte)seed.Damage,item);
                FailIf(!bee.ApplySeedCollision(actor.CollisionBounds,actor.Position-Vector2.Right,seed,collision,spawns).Contact,"Child bee rejected an eligible native seed.");
            }
            foreach(var spawn in spawns)FailIf(!_entities.TryCreatePart(spawn),"Child bee fixture requires a free PART slot.");
            FailIf(actor.Health!=rom[0xd0a9]||(actor.InvincibilityCounter&255)!=rom[0xd0ab]||actor.StunCounter!=rom[0xd0ae]||actor.State!=rom[0xd084],"Child bee collision must publish native damage, stun and state before the next update.");
            Step(4);
            if(collision==0x1d)
            {
                // Check initialization, a paused dispatch and the last stun
                // boundary without spending 480 updates waiting out Pegasus.
                _dialogue.ShowMessage("Bee stun pause.",_player.Position.Y);rom[0xcba0]=1;Step(2);_dialogue.Close();rom[0xcba0]=0;
                typeof(VeranChildBeeCharacter).GetField("_stun",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,3);rom[0xd0ae]=3;Step(10);
            }
            else Step(collision is 0x1b or 0x1e?116:28);
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranChildBeeRoomEntity>().Any()||_entities.EntityAdapters<BurningEnemyRoomEntity>().Any(),"Room replacement must retire the child bee and its native flame.");
            FailIf(reservations.Any(reservation=>GodotObject.IsInstanceValid(reservation.Node)&&!reservation.Node.IsQueuedForDeletion()),
                "Room replacement must release the full-PART fixture's owned reservation nodes.");
        }
    }
}
