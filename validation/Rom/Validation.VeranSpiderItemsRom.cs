using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranSpiderItemsRom()
    {
        try
        {
        foreach(bool batched in new[]{false,true})
        foreach(var spec in new (int Collision,int Health,int Cutscene)[] {(0x1a,4,0),(0x1b,4,0),(0x1b,1,0),
            (0x1c,4,0),(0x1d,4,0),(0x1e,4,0),(0x1e,4,1),(0x1e,4,12),(0x1e,4,34),
            (4,4,0),(5,4,0),(6,4,0),(7,4,0),(8,4,0),(9,4,0),(11,4,0),(0x16,4,0),(0x18,4,0),(0x19,4,0)})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xfc);_entities.Clear();
            // Keep the declared camera at zero in both executions; camera
            // following is independent of this enemy/item status matrix.
            _entities.WorldToScreen=static position=>position;
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x78,0x98));
            var spider=(VeranSpiderRoomEntity)_entities.TryCreateVeranSpider(new(0x78,0x58))!;
            var actor=spider.Actor;var initial=_random.CaptureState();
            var rom=new SomariaRom(_saveData,initial,_currentRoom,0,0x78,0x98)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();rom[0xd080]=1;rom[0xd081]=0x0f;rom[0xd082]=1;rom[0xcdd1]=1;
            rom[0xd08b]=0x58;rom[0xd08d]=0x78;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;
            void Compare()
            {
                rom.UpdateGameplay(0,0,0xff,++update);
                bool dead=rom[0xd080]==0;
                FailIf(spider.Finished!=dead||!dead&&(actor.Health!=rom[0xd0a9]||
                    (actor.InvincibilityCounter&255)!=rom[0xd0ab]||actor.StunCounter!=rom[0xd0ae]||
                    actor.KnockbackCounter!=rom[0xd0ad]||actor.State!=rom[0xd084]||
                    !spider.GaleCaught&&(actor.ZFixed!=unchecked((short)rom.Word(0xd08e))||
                    actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f))),
                    $"Veran spider seed ${spec.Collision:x2}, HP{spec.Health}, cfd0@{spec.Cutscene}, update{update}: " +
                    $"port dead/HP/inv/stun/recoil/state/Z/XY={spider.Finished}/{actor.Health}/{actor.InvincibilityCounter}/{actor.StunCounter}/{actor.KnockbackCounter}/{actor.State}/{actor.ZFixed}/{actor.Position}; " +
                    $"ROM={dead}/{rom[0xd0a9]}/{rom[0xd0ab]}/{rom[0xd0ae]}/{rom[0xd0ad]}/{rom[0xd084]}/{unchecked((short)rom.Word(0xd08e))}/{rom.Word(0xd08c)/256f},{rom.Word(0xd08a)/256f}.");
                FailIf(!dead&&spider.GaleCaught&&(actor.Position!=new Vector2(rom[0xd08d],rom[0xd08b])||
                    actor.GaleDrawZ!=unchecked((sbyte)rom[0xd08f])),
                    $"Veran spider Gale XY/Z high bytes differ on update {update}.");
                FailIf(RandomDiffers(initial, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    _entities.RoomEnemyCount!=rom[0xcdd1]||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Veran spider seed ${spec.Collision:x2}, cfd0@{spec.Cutscene}, update{update}: RNG={_random.CaptureState().Calls-initial.Calls}/{rom.RandomCalls}, " +
                    $"count={_entities.RoomEnemyCount}/{rom[0xcdd1]}, sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                foreach(var puff in _entities.EntityAdapters<DeathPuffRoomEntity>())
                {
                    var effect=(EnemyDeathPuffEffect)puff.Node;
                    int slot=Enumerable.Range(0,16).Single(slot=>ReferenceEquals(_entities.ResolvePartSlot(slot),puff));
                    int address=0xd0c0+slot*256;
                    FailIf(rom[address+1]!=2||effect.Position!=new Vector2(rom[address+13],rom[address+11])||
                        effect.ZHigh!=unchecked((sbyte)rom[address+15]),
                        $"Veran spider death puff must copy high X/Y/Z independently in PART slot ${address:x4}.");
                }
            }
            for(int wait=0;wait<160&&actor.State!=9;wait++)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(actor.State!=9,"Veran spider seed fixture must reach its source landing before applying an item.");
            actor.Health=spec.Health;rom[0xd0a9]=(byte)spec.Health;
            var spawns=new List<RoomEntitySpawn>();
            if(spec.Collision<0x1a)
            {
                int damage=spec.Collision is 5 or 8?2:spec.Collision is 6 or 7 or 11 or 0x18?4:1;
                rom.HitEnemyWithItem(0,spec.Collision,damage);
                FailIf(!spider.ApplyItemCollision((RoomEntityItemCollision)spec.Collision,actor.CollisionBounds,
                    actor.Position.Floor()-Vector2.Right,damage,spawns),$"Veran spider item ${spec.Collision:x2} rejected its source collision.");
            }
            else
            {
            int item=spec.Collision switch {0x1a=>ItemId.MysterySeed,0x1b=>ItemId.EmberSeed,
                0x1c=>ItemId.ScentSeed,0x1d=>ItemId.PegasusSeed,_=>ItemId.GaleSeed};
            FailIf(!new SeedSatchelDatabase().TryGet(item,out var seed),$"Missing seed ${item:x2}.");
            rom.HitEnemyWithItem(0,spec.Collision,-(sbyte)seed.Damage,item);
            FailIf(!spider.ApplySeedCollision(actor.CollisionBounds,actor.Position.Floor()-Vector2.Right,seed,spec.Collision,spawns).Contact,
                $"Veran spider must accept source seed collision ${spec.Collision:x2}.");
            }
            foreach(var spawn in spawns)FailIf(!_entities.TryCreatePart(spawn),"Spider fixture requires one free native PART slot.");
            FailIf(actor.Health!=rom[0xd0a9]||(actor.InvincibilityCounter&255)!=rom[0xd0ab]||actor.StunCounter!=rom[0xd0ae]||
                actor.KnockbackCounter!=rom[0xd0ad]||actor.State!=rom[0xd084],
                $"Veran spider seed ${spec.Collision:x2} must publish its source damage/stun/state before the next enemy update.");
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            if(spec.Cutscene!=0)
            {
                Step(spec.Cutscene-1);_runtimeState.SetWramByte(0xcfd0,1);rom[0xcfd0]=1;
                Step(151-spec.Cutscene);
            }
            else Step(150);
            if(spec.Cutscene!=0)FailIf(!actor.CutsceneDeath||!spider.Finished,
                "cfd0 must override Gale's pending-hit, shake and airborne flight states.");
        }
        }
        finally {_entities.WorldToScreen=_transitions.WorldToGameplayScreen;}
    }
}
