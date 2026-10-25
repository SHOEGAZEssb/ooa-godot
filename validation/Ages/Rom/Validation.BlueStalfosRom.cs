using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBlueStalfosRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x82);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(120, 152));
            FailIf(_collision.Collides(_player.Position), "Blue Stalfos fixture must start on original south floor in room$5:$82.");
            var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(e => e.Node is not BlueStalfosCharacter && e is not DungeonRewardRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities, [entity]); }
            typeof(Player).GetField("_enemyInvincibilityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_player, 100000f);
            var slots = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities, "_enemySlots");
            var parts = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities, "_partSlots");
            FailIf(slots.Count != 1 || slots.Single().Value != 0 || _entities.RoomEnemyCount != 1 ||
                _entities.Entities<BlueStalfosCharacter>().Single() is not { Health: 18, Position: var position } || position != new Vector2(120,0),
                "D8 Blue Stalfos must begin as one counted spawner$77:$00 with HP$12 at Y$00/X$78 in enemy slot$00.");
            var seed = _random.CaptureState(); var rom = new EnemyStatusRom(_currentRoom, _saveData, seed.Calls);
            foreach (var (header, index) in new byte[] { 0xb9,0xba,0xbb,0x8e,0xa7,0x78 }.Select((header,index)=>(header,index))) rom[0xcc08 + index * 2] = header;
            rom[0xcc39] = 8; rom[0xcdd1] = 1;
            rom[0xd080] = 0x11; rom[0xd081] = 0x77; rom[0xd08d] = 120;
            var reward = active.OfType<DungeonRewardRoomEntity>().Single(); int rewardSlot = 0xd040 + _entities.InteractionSlot(reward) * 256;
            rom[rewardSlot] = 1; rom[rewardSlot + 1] = 0x20; rom[rewardSlot + 2] = 0;
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            int update = 0; var states = new HashSet<int>(); int projectiles = 0, batEntries = 0;
            void Compare()
            {
                rom.Update(_entities.FrameCounter, _player.EnemyContactPosition); update++;
                foreach (var (entity, slot) in slots)
                {
                    var actor = (BlueStalfosCharacter)entity.Node; int address = 0xd080 + slot * 256;
                    if (actor.Record.SubId == 1) states.Add(actor.State);
                    if (actor.Record.SubId == 1 && actor.State == 22 && actor.Counter1 == 240) batEntries++;
                    FailIf(rom[address] == 0 || actor.Record.SubId != rom[address + 2] || actor.State != rom[address + 4] ||
                        actor.Counter1 != rom[address + 6] || actor.Counter2 != rom[address + 7] || actor.Speed != rom[address + 16] ||
                        actor.Angle != rom[address + 9] || actor.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        unchecked((ushort)actor.ZFixed) != rom.Word(address + 14) || actor.NativeVisible != rom[address + 26] ||
                        actor.InvincibilityCounter != unchecked((sbyte)rom[address + 43]),
                        $"Blue Stalfos slot${slot:x2} update{update}: runtime sub/state/counters/speed/angle/XY/Z/visible/inv={actor.Record.SubId}/{actor.State}/{actor.Counter1},{actor.Counter2}/${actor.Speed:x2}/${actor.Angle:x2}/{actor.Position}/{actor.ZFixed}/${actor.NativeVisible:x2}/{actor.InvincibilityCounter}; ROM={rom[address+2]}/{rom[address+4]}/{rom[address+6]},{rom[address+7]}/${rom[address+16]:x2}/${rom[address+9]:x2}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{rom.Word(address+14)}/${rom[address+26]:x2}/{unchecked((sbyte)rom[address+43])}.");
                    if (actor.Record.SubId == 1)
                        FailIf(actor.ProjectilePatternIndex != rom[address + 0x32] || actor.AnimationParameter != rom[address + 0x21] ||
                            actor.Target != new Vector2(rom[address + 0x31], rom[address + 0x30]),
                            $"Blue Stalfos update{update}: target, pattern index or main animation parameter differs.");
                }
                foreach (var (entity, slot) in parts.Where(pair => pair.Key.Node is BlueStalfosProjectile))
                {
                    var shot = (BlueStalfosProjectile)entity.Node; int address = 0xd0c0 + slot * 256;
                    if (shot.State == 1 && shot.Counter1 == 40 && shot.Counter2 == 0) projectiles++;
                    FailIf(rom[address] == 0 || shot.SubId != rom[address+2] || shot.State != rom[address+4] ||
                        shot.Counter1 != rom[address+6] || shot.Counter2 != rom[address+7] || shot.Angle != rom[address+9] || shot.Speed != rom[address+16] ||
                        shot.Position != new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        shot.Animation.CurrentParameter != rom[address+0x21] || shot.Visible != ((rom[address+26] & 0x80) != 0),
                        $"Blue Stalfos PART$3d slot${slot:x2} update{update}: runtime state/counters/speed/angle/XY/parameter={shot.State}/{shot.Counter1},{shot.Counter2}/{shot.Speed}/{shot.Angle}/{shot.Position}/{shot.Animation.CurrentParameter}; ROM={rom[address+4]}/{rom[address+6]},{rom[address+7]}/{rom[address+16]}/{rom[address+9]}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}/{rom[address+0x21]}.");
                }
                var rng = _random.CaptureState();
                int nativeParts = Enumerable.Range(0,16).Count(slot => rom[0xd0c0+slot*256] != 0 && rom[0xd0c1+slot*256] == 0x3d);
                FailIf(nativeParts != parts.Keys.Count(entity => entity.Node is BlueStalfosProjectile),
                    $"Blue Stalfos update{update}: native PART$3d allocation closure differs.");
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls-seed.Calls != rom.RandomCalls ||
                    _entities.RoomEnemyCount != rom[0xcdd1], $"Blue Stalfos update{update}: ordered allocation count or global RNG differs.");
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batch, afterUpdate: Compare);
            Step(650);
            FailIf(!new[] { 8,9,10,11,12,14,15 }.All(states.Contains) || projectiles < 2,
                "Bounded Blue Stalfos trace must exercise intro, quadrant travel, charge/release and both ball pattern types.");
            BlueStalfosCharacter Main() => _entities.Entities<BlueStalfosCharacter>().Single(actor => actor.Record.SubId == 1);
            IRoomEntity? incoming = null;
            for (int wait = 0; wait < 650 && incoming is null; wait++)
            {
                var main = Main();
                incoming = parts.Keys.FirstOrDefault(entity => entity.Node is BlueStalfosProjectile
                    { SubId: 0, NonReflectable: false, State: 2 or 3 } shot && main.State == 15 && main.Counter1 > 20 &&
                    shot.Position.Y >= main.Position.Y + 8 && shot.Position.Y <= main.Position.Y + 32);
                if (incoming is null) Step(1);
            }
            FailIf(incoming is null, "Blue Stalfos must emit a reflectable ball while its main body pauses after firing.");
            var ball = (BlueStalfosProjectile)incoming!.Node; int ballSlot = parts[incoming];
            rom.HitPartWithItem(ItemCollisionType.L1Sword, ballSlot);
            ((ILinkSwordStateAwareRoomEntity)incoming).SetLinkSwordState(SwordActionState.Swing, 1);
            FailIf(!((ISwordHittableRoomEntity)incoming).ApplySwordHit(ball.CollisionBounds, ball.Position - Vector2.Down, 1,
                EnemyKnockbackStrength.Normal, []), "The source baby ball must accept a sword reflection without ordinary projectile damage.");
            int before = Main().Health;
            Step(40);
            FailIf(Main().Health != before || Main().State is not (21 or 22) || !ball.Finished,
                "A reflected baby ball must hit its linked boss, preserve HP, then enter the puff/bat transformation.");
            Step(25);
            var bat = Main();
            FailIf(bat.State != 22 || bat.CollisionMode != 0x65 || bat.CollisionBounds.Size != new Vector2(12,4),
                "Blue Stalfos's vulnerable bat must use mode$65 and native radiiY$02/X$06.");
            FailIf(batEntries!=1,"The vulnerable bat window must begin with the source counter$F0 (240 updates).");
            Step(bat.Counter1-1);
            FailIf(bat.State!=22 || bat.Counter1!=1,"Blue Stalfos must retain the vulnerable bat through its final nonzero countdown.");
            Step(1);
            FailIf(bat.State!=23 || bat.CollisionMode!=0x48,"The 240th bat update must begin the restoration puff and restore body collision mode$48.");
            Step(25);
            FailIf(bat.State is 22 or 23 || bat.CollisionBounds.Size!=new Vector2(16,16),"Completed bat restoration must recover the ordinary body's hitbox and attacks.");
            incoming=null;
            for(int wait=0;wait<1200 && incoming is null;wait++)
            {
                incoming=parts.Keys.FirstOrDefault(entity=>entity.Node is BlueStalfosProjectile
                    {SubId:0,NonReflectable:false,State:2 or 3} shot && bat.State==15 && bat.Counter1>20 &&
                    shot.Position.Y>=bat.Position.Y+8 && shot.Position.Y<=bat.Position.Y+32);
                if(incoming is null) Step(1);
            }
            FailIf(incoming is null,"Restored Blue Stalfos must repeat its reflectable-ball attack.");
            ball=(BlueStalfosProjectile)incoming!.Node; ballSlot=parts[incoming];
            rom.HitPartWithItem(4,ballSlot);
            ((ILinkSwordStateAwareRoomEntity)incoming).SetLinkSwordState(SwordActionState.Swing,1);
            FailIf(!((ISwordHittableRoomEntity)incoming).ApplySwordHit(ball.CollisionBounds,ball.Position-Vector2.Down,1,
                EnemyKnockbackStrength.Normal,[]),"The repeated baby-ball reflection must remain available after restoration.");
            Step(65);
            FailIf(bat.State!=22 || batEntries!=2,"The repeated reflection must reopen the source bat-damage window.");
            // Exercise one real damage exchange and the shared death/reward
            // handoff with declared final HP, rather than replaying 18 hits.
            bat.Health = 1; int batSlot = slots.Single(pair => pair.Key.Node == bat).Value;
            rom[0xd080 + batSlot * 256 + 0x29] = 1;
            rom.HitWithSword(ItemCollisionType.L1Sword, 1, batSlot);
            var batEntity = slots.Single(pair => pair.Value == batSlot).Key;
            ((ILinkSwordStateAwareRoomEntity)batEntity).SetLinkSwordState(SwordActionState.Swing, 1);
            FailIf(!((ISwordHittableRoomEntity)batEntity).ApplySwordHit(bat.CollisionBounds, bat.Position - Vector2.Right,
                1, EnemyKnockbackStrength.Normal, []), "The vulnerable bat must accept the source sword damage exchange.");
            Step(220);
            FailIf(!bat.IsDead || _entities.RoomEnemyCount != 0 || !_saveData.HasRoomFlag(5, 0x82, 0x80),
                "Blue Stalfos's counted death explosion must release the shared miniboss completion flag and portal handoff.");
        }
        GD.Print("Validated clean-US Blue Stalfos ordered spawner/body/sickle/afterimage slots, intro/routes, charged projectile patterns and shared RNG through gameplay.");
    }
}
