using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranBossItemsRom()
    {
        // Each distinct remaining response, without multiplying weapon rows
        // by forms/health tiers/schedules. Blocked recoil has its own fixture.
        foreach(var spec in new (bool Fairy,int Mode,int Item,int Effect,bool Masked)[]{
            (true,0x0d,4,0x21,false),(true,0x0d,13,0x1c,false),
            (true,0x0d,0x1a,0x20,false),(true,0x0d,0x1b,0x21,false),
            (false,0x6a,4,0,false),(false,0x6a,0x18,0,false),
            (false,9,4,0x21,false),(false,9,9,0x1c,false),
            (false,9,0x19,0x20,false),(false,0x6b,0x1c,0x21,false),
            (false,0x6c,0x1e,0x20,false),(false,9,4,0,true)})
        {
            ReinitializeGameplayForValidation();int room=spec.Fairy?0xfc:0xd4;
            if(spec.Fairy)_saveData.SetRoomFlag(4,room,0x80);
            LoadValidationRoom(4,room);_inventory.EquipA(0);_inventory.EquipB(0);
            IPostObjectItemCollisionRoomEntity target;EnemyCharacter actor;
            if(spec.Fairy)
            {
                _entities.Clear();var fairy=_roomEvents.Get<BlackTowerPossessionEvent>().SpawnFairy(new(120,64))!;
                target=fairy;actor=fairy.Actor;
            }
            else
            {
                var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();target=boss;actor=boss.Actor;
                foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            }
            _player.WarpTo(new(120,136));_transitions.ResetCamera();
            var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,0,120,136)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=(byte)(spec.Fairy?0xd4:0xd7);rom[0xcc0a]=(byte)(spec.Fairy?0xd5:0xd8);
            rom[0xcc0c]=(byte)(spec.Fairy?0xd6:0xd9);rom[0xcc0e]=(byte)(spec.Fairy?0x8e:0xda);
            rom[0xd080]=1;rom[0xd081]=(byte)(spec.Fairy?6:2);rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;
            void Compare()
            {
                rom.UpdateGameplay(0,0,255,++update);var random=_random.CaptureState();
                FailIf(actor.Health!=rom[0xd0a9]||(actor.InvincibilityCounter&255)!=rom[0xd0ab]||actor.KnockbackCounter!=rom[0xd0ad]||
                    (spec.Fairy?((VeranFairyCharacter)actor).JustHit:((VeranFinalCharacter)actor).JustHit)!=((rom[0xd0aa]&128)!=0)||
                    actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)||
                    random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||random.Calls-initial.Calls!=rom.RandomCalls||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Veran fairy{spec.Fairy}, mode${spec.Mode:x2}, item${spec.Item:x2}, update{update}: source health/inv/recoil/pending/movement/RNG/sound differs.");
                if(spec.Fairy)
                {
                    var fairy=(VeranFairyCharacter)actor;
                    FailIf(fairy.State!=rom[0xd084]||fairy.StillCounter!=rom[0xd0b8]||fairy.HealthTier!=rom[0xd0b5]||fairy.Speed!=rom[0xd090],
                        "Fairy JUST_HIT must update the native health tier/speed before its ordinary state dispatch.");
                }
                else
                {
                    var boss=(VeranFinalCharacter)actor;
                    FailIf(boss.State!=rom[0xd084]||boss.Counter!=rom[0xd086]||boss.TurtleHit!=rom[0xd0b3],
                        "Final-boss JUST_HIT must preserve its source turtle transformation signal.");
                }
            }
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            void Set(string name,int value)=>actor.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(actor,value);
            // Native waiting states isolate collision publication, its next
            // dispatch and invincibility expiry from unrelated attack cycles.
            actor.Health=20;rom[0xd0a9]=20;
            if(spec.Fairy){Set("State",4);Set("StillCounter",90);rom[0xd084]=4;rom[0xd0b8]=90;}
            else
            {
                Set("State",7);Set("Counter",90);Set("CollisionMode",spec.Mode);rom[0xd084]=7;rom[0xd086]=90;rom[0xd0a5]=(byte)spec.Mode;
                if(spec.Masked){Set("CollisionType",0x16);rom[0xd0a4]=0x96;}
            }
            Vector2 origin=actor.Position-Vector2.Right;var spawns=new List<RoomEntitySpawn>();
            int itemId=spec.Item switch {0x1a=>ItemId.MysterySeed,0x1b=>ItemId.EmberSeed,0x1c=>ItemId.ScentSeed,0x1e=>ItemId.GaleSeed,_=>0};
            // Native ITEM$20/$21/$24 damage is $fe; Gale is $ff.
            int damage=itemId is ItemId.EmberSeed or ItemId.ScentSeed or ItemId.MysterySeed?2:1;
            void Hit()
            {
                rom.HitEnemyWithItem(0,spec.Item,damage,itemId);
                bool accepted;
                if(itemId==0)accepted=target.ApplyItemCollision((RoomEntityItemCollision)spec.Item,actor.CollisionBounds,origin,1,spawns);
                else
                {
                    FailIf(!new SeedSatchelDatabase().TryGet(itemId,out var seed),"Missing boss seed fixture.");
                    var result=((ISeedCollisionTarget)target).ApplySeedCollision(actor.CollisionBounds,origin,seed,spec.Item,spawns);accepted=result.Contact;
                    FailIf(result.DisableCollision!=(spec.Effect==0x20)||result.Effect!=(itemId==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate),
                        "Boss seed must retain native activation/randomization and collision-disable response.");
                }
                int health=spec.Effect==0x21?20-damage:20;
                FailIf(accepted==spec.Masked||actor.Health!=health||rom[0xd0a9]!=health||
                    actor.InvincibilityCounter!=(spec.Effect==0x21?32:0)||rom[0xd0ab]!=actor.InvincibilityCounter||
                    (spec.Fairy?((VeranFairyCharacter)actor).JustHit:((VeranFinalCharacter)actor).JustHit)!=(spec.Effect!=0)||
                    ((rom[0xd0aa]&128)!=0)!=(spec.Effect!=0),
                    $"Veran mode${spec.Mode:x2}, item${spec.Item:x2}: expected effect${spec.Effect:x2}, HP{health}, source hit/inv/mask.");
            }
            Hit();StepGameplayUpdates(spec.Effect==0x21?32:2,Vector2.Zero,batched:spec.Item==4,afterUpdate:Compare);
            FailIf(actor.InvincibilityCounter!=0,"Boss damage invincibility must expire after32 eligible updates.");
            actor.Health=20;rom[0xd0a9]=20;Hit();StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranFairyRoomEntity>().Any()||_entities.EntityAdapters<VeranFinalRoomEntity>().Any(),"Boss item cancellation retained its actor.");
        }
    }

    private void ValidateVeranBossSomariaRom()
    {
        foreach(bool fairyForm in new[]{true,false})
        {
            ReinitializeGameplayForValidation();int room=fairyForm?0xfc:0xd4;
            if(fairyForm)_saveData.SetRoomFlag(4,room,0x80);
            LoadValidationRoom(4,room);EnemyCharacter actor;
            if(fairyForm){_entities.Clear();actor=_roomEvents.Get<BlackTowerPossessionEvent>().SpawnFairy(new(120,64))!.Actor;}
            else
            {
                var boss=_entities.EntityAdapters<VeranFinalRoomEntity>().Single();actor=boss.Actor;
                foreach(var other in _entities.EntityAdapters<IRoomEntity>().Where(entity=>entity!=boss).ToArray())_entities.RetireEntity(other);
            }
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria,1);_inventory.EquipA(TreasureId.CaneOfSomaria);_inventory.EquipB(0);
            _player.WarpTo(new(120,112));_player.Face(Vector2I.Up);_transitions.ResetCamera();
            FailIf(_collision.Collides(_player.Position),"Boss Cane use must approach on the real arena floor.");
            var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,0,120,112)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xcc08]=(byte)(fairyForm?0xd4:0xd7);rom[0xcc0a]=(byte)(fairyForm?0xd5:0xd8);
            rom[0xcc0c]=(byte)(fairyForm?0xd6:0xd9);rom[0xcc0e]=(byte)(fairyForm?0x8e:0xda);
            rom[0xd080]=1;rom[0xd081]=(byte)(fairyForm?6:2);rom[0xd08b]=64;rom[0xd08d]=120;rom[0xcdd1]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0,pressed=0;bool hit=false,sawBlock=false;
            void Compare()
            {
                rom.UpdateGameplay(pressed,pressed,255,++update);pressed=0;rom.AdvanceTileGraphics();
                var blocks=_entities.EntityAdapters<SomariaBlockRoomEntity>().ToArray();
                FailIf(blocks.Length!=rom.Blocks.Length,"Boss Somaria allocation/deletion must retain native item slots.");
                foreach(var block in blocks)
                {
                    int at=Enumerable.Range(0,5).Select(slot=>0xd700+slot*256).Single(slot=>rom[slot]!=0&&rom[slot+1]==0x18);
                    sawBlock=true;hit|=(block.Block.Flags&0x20)!=0;
                    FailIf(block.Block.State!=rom[at+4]||block.Block.Flags!=rom[at+0x2f]||block.Block.Position!=new Vector2(rom[at+13],rom[at+11]),
                        "Boss effect$2d must flag the live Somaria block in the post-object pass.");
                }
                var random=_random.CaptureState();
                FailIf(actor.Health!=rom[0xd0a9]||actor.InvincibilityCounter!=0||rom[0xd0ab]!=0||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||random.Calls-initial.Calls!=rom.RandomCalls||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Veran fairy{fairyForm} Cane update{update}: Link/health/inv/RNG/sound differs.");
            }
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            // Freeze only the initialized enemy, retaining its native collision
            // identity while the actual Cane and block owners keep updating.
            actor.Position=new(120,88);rom.Word(0xd08a,88*256);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,4);rom[WramAddress.wDisabledObjects]=4;
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled,0);rom[WramAddress.wMenuDisabled]=0;
            for(int attempt=0;attempt<2;attempt++)
            {
                hit=sawBlock=false;pressed=1;StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],afterUpdate:Compare);
                StepGameplayUpdates(32,Vector2.Zero,batched:!fairyForm,afterUpdate:Compare);
                FailIf(!sawBlock||!hit||_entities.EntityAdapters<SomariaBlockRoomEntity>().Any(),
                    $"Cane fairy{fairyForm} attempt{attempt}: saw{ sawBlock}, hit{hit}, remaining{_entities.EntityAdapters<SomariaBlockRoomEntity>().Count()}, Link{_player.Position}; must reach Veran, publish flag$20 and delete.");
            }
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Any(),"Cancelling the boss must leave no Cane block.");
        }
    }
}
