using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTargetGhiniRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x9b);
            _inventory.SetScriptedEquippedItems(0, 0);
            Vector2 floor = Enumerable.Range(0, 0xb0)
                .Select(p => new Vector2((p & 15) * 16 + 8, (p >> 4) * 16 + 8))
                .First(p => !_collision.Collides(p) && _currentRoom.GetTerrainInfo(p).Hazard == HazardType.None);
            _player.WarpTo(floor);
            typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 100000f);
            var slots = SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_enemySlots");
            FailIf(slots.Count != 3 || slots.Keys.Any(e => e.Node is not TargetGhiniCharacter) || _entities.RoomEnemyCount != 3,
                "5:9b requires three counted ENEMY$17:$02 from obj_RandomEnemy $64/$17/$02.");
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc08] = 0x90; rom[0xcdd1] = 3; rom[0xcd00] = 1;
            // Retain $13:$01's synthetic enemy count; reserve only $20:$05,
            // whose unmodified stair commands are exercised separately.
            foreach (var pair in SomariaPrivate<Dictionary<IRoomEntity, int>>(_entities, "_interactionSlots"))
            {
                int address = 0xd040 + pair.Value * 256;
                rom[address] = 1;
                if (pair.Key is PushBlockTriggerRoomEntity)
                {
                    rom[address + 1] = 0x13; rom[address + 2] = 1;
                    rom[address + 11] = 0x48; rom[address + 13] = 0x68;
                }
                else { rom[address + 1] = 0x1d; rom[address + 4] = 1; }
            }
            var enemies = slots.OrderBy(pair => pair.Value).ToArray();
            foreach (var pair in enemies)
            {
                int address = 0xd080 + pair.Value * 256;
                rom[address] = (byte)(((pair.Value + 1) << 4) | 1);
                rom[address + 1] = 0x17; rom[address + 2] = 2;
                rom.Word(address + 10, (int)(pair.Key.Node.Position.Y * 256));
                rom.Word(address + 12, (int)(pair.Key.Node.Position.X * 256));
            }
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            bool text = true; _entities.TextActiveSource = () => text;
            int update = 0, decelerating = 0, targets = 0;
            void Compare()
            {
                rom[0xcba0] = (byte)(text ? 1 : 0); rom.Update(_entities.FrameCounter, _player.Position);
                foreach (var pair in enemies)
                {
                    var enemy = (TargetGhiniCharacter)pair.Key.Node;
                    int address = 0xd080 + pair.Value * 256;
                    int timer = SomariaPrivate<int>(enemy.Animation, "_frameCounter");
                    FailIf(enemy.State != rom[address + 4] || enemy.Counter != rom[address + 6] ||
                        enemy.Speed != rom[address + 16] || enemy.Angle != rom[address + 9] ||
                        enemy.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        enemy.ZFixed != unchecked((short)rom.Word(address + 14)) || enemy.SpeedZ != unchecked((short)rom.Word(address + 0x14)) ||
                        enemy.TargetPosition != new Vector2(rom[address + 0x31], rom[address + 0x30]) ||
                        enemy.StunCounter != rom[address + 0x2e] || enemy.KnockbackCounter != rom[address + 0x2d] ||
                        (enemy.InvincibilityCounter & 255) != rom[address + 0x2b] || enemy.Health != rom[address + 0x29] ||
                        enemy.Visible != ((rom[address + 0x1a] & 0x80) != 0) ||
                        enemy.CollisionEnabled != ((rom[address + 0x24] & 0x80) != 0) ||
                        timer != rom[address + 0x20] || enemy.AnimationParameter != rom[address + 0x21],
                        $"ENEMY$17:$02 slot${pair.Value:x2} update{update}, batch={batched}: " +
                        $"runtime state/counter/speed/angle={enemy.State}/{enemy.Counter}/{enemy.Speed}/{enemy.Angle}, XY={enemy.Position}, target={enemy.TargetPosition}, Z={enemy.ZFixed}/{enemy.SpeedZ}, stun={enemy.StunCounter}, anim={timer}/{enemy.AnimationParameter}; " +
                        $"ROM={rom[address + 4]}/{rom[address + 6]}/{rom[address + 16]}/{rom[address + 9]}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, target={rom[address + 0x31]}/{rom[address + 0x30]}, Z=${rom.Word(address + 14):x4}/${rom.Word(address + 0x14):x4}, stun={rom[address + 0x2e]}, anim={rom[address + 0x20]}/{rom[address + 0x21]}.");
                    if (enemy.State == 11) decelerating++;
                    if (enemy.State == 9 && enemy.Counter == 0x23) targets++;
                }
                var actual = _random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls - seed.Calls != rom.RandomCalls ||
                    _entities.RoomEnemyCount != rom[0xcdd1], $"ENEMY$17:$02 update{update}: shared RNG/count differs: runtime=${actual.Rng2:x2}{actual.Rng1:x2}/{actual.Calls - seed.Calls}, count={_entities.RoomEnemyCount}; ROM=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}, count={rom[0xcdd1]}, triggerstate={rom[0xd244]}, enabled={rom[0xd240]}.");
                update++;
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(3); text = false; Step(450); text = true; Step(3); text = false; Step(100);
            FailIf(decelerating == 0 || targets < 9, "ENEMY$17:$02 must execute several target/acceleration/arrival/deceleration cycles.");
            var first = (TargetGhiniCharacter)enemies[0].Key.Node;
            int firstAddress = 0xd080 + enemies[0].Value * 256;
            rom.HitWithSword(ItemCollisionType.L1Sword, 1);
            FailIf(!((IItemCollisionHittableRoomEntity)enemies[0].Key).ApplyItemCollision(
                (RoomEntityItemCollision)ItemCollisionType.L1Sword, first.CollisionBounds, first.Position.Floor() - Vector2.Right,
                1, new List<RoomEntitySpawn>()), "ENEMY$17:$02 must accept native sword recoil.");
            first.DeferNativeHitStatus();
            Step(40);
            // Source column$17 is collisionEffect22: odd-update stun with no
            // JUST_HIT. Supply the result of that actual native collision.
            rom.HitWithSword(ItemCollisionType.L1Boomerang, 0);
            first.ApplyBoomerangStun(120); first.InvincibilityCounter = -16;
            Step(245);
            FailIf(first.StunCounter != 0 || first.ZFixed != unchecked((short)rom.Word(firstAddress + 14)),
                "ENEMY$17:$02 stun completion must restore its hovering Z high byte $fe.");

            var adapter = (TargetGhiniRoomEntity)enemies[0].Key;
            var spawns = new List<RoomEntitySpawn>();
            Vector2 origin = first.Position.Floor() - Vector2.Right;
            bool rejectedMystery = false;
            try { adapter.ApplySeedHit(first.CollisionBounds, origin, ItemId.MysterySeed, spawns); }
            catch (System.InvalidOperationException ex) { rejectedMystery = ex.Message.Contains("$17") && ex.Message.Contains("live collision type"); }
            FailIf(!rejectedMystery || adapter.ApplySeedHit(first.CollisionBounds, origin, -1, spawns) != SeedHitResult.None,
                "ENEMY$17:$02 seed-item entry must reject unresolvable ITEM24 identity and ignore unknown seed IDs.");
            // objectCollisionTable mode$1a/column$1d selects effect$28. Run
            // its native scan, then compare status and recovery in the real loop.
            rom[0xd701] = ItemId.PegasusSeed;
            rom.HitWithSword(0x1d, 1);
            FailIf(adapter.ApplySeedHit(first.CollisionBounds, origin, ItemId.PegasusSeed, spawns) != SeedHitResult.Activate || spawns.Count != 0,
                "ENEMY$17:$02 Pegasus effect$28 must publish status without allocating PART$12.");
            FailIf(first.StunCounter != 0xf0 || first.InvincibilityCounter != -16,
                "collisionEffects.s ENEMYDMG_38 writes stun$f0 and signed invincibility$f0.");
            new SeedSatchelDatabase().TryGet(ItemId.PegasusSeed, out var pegasus);
            FailIf(adapter.ApplySeedCollision(first.CollisionBounds, origin, pegasus, 0x1d, spawns).Contact,
                "ENEMY$17:$02 invincibility must reject a repeated seed collision.");
            text = true; Step(3); text = false; Step(485);
            FailIf(first.StunCounter != 0 || first.InvincibilityCounter != 0,
                "ENEMY$17:$02 Pegasus stun and invincibility must finish after eligible updates resume.");

            first.Health = 1; rom[firstAddress + 0x29] = 1;
            rom[0xd701] = ItemId.EmberSeed;
            rom.HitWithSword(ItemCollisionType.EmberSeed, 1);
            new SeedSatchelDatabase().TryGet(ItemId.EmberSeed, out var ember);
            FailIf(!adapter.ApplySeedCollision(first.CollisionBounds, first.Position.Floor() - Vector2.Right,
                ember, ItemCollisionType.EmberSeed, new List<RoomEntitySpawn>()).Contact,
                "ENEMY$17:$02 must accept its native Ember effect$27.");
            _entities.Spawn<BurningEnemyPart>(new BurningEnemySpawn(adapter));
            Step(59);
            FailIf(first.Health != 0 || first.CollisionEnabled || first.StunCounter != 1 ||
                _entities.Entities<BurningEnemyPart>().Any(),
                "ENEMY$17:$02 lethal Ember must retain disabled collisions while PART$12 borrows health$01, then restore zero health and stun$01 after 59 updates.");

            int beforeScroll = _random.Calls;
            _entities.BeginScreenTransition(5, _currentRoom, new(240, 0), _player);
            var incoming = _entities.Entities<TargetGhiniCharacter>().ToArray();
            FailIf(incoming.Length != 3 || _random.Calls - beforeScroll != 259 ||
                incoming.Any(e => e.State != 8 || e.Speed != 0x14 || e.Counter != 0 || e.ZFixed != -0x200 || !e.Visible),
                "ENEMY$17:$02 scrolling preload must consume the 256-value placement buffer and three common state0 RNG values, then publish visible state$08 without choosing targets.");
            var positions = incoming.Select(e => e.Position).ToArray();
            int calls = _random.Calls;
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(_random.Calls != calls || !incoming.Select(e => e.Position).SequenceEqual(positions) ||
                incoming.Any(e => e.State != 8 || e.Counter != 0),
                "Destination ENEMY$17:$02 must remain frozen during scrolling.");
            _entities.FinishScreenTransition();
            StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            FailIf(incoming.Any(e => e.State != 9 || e.Counter != 0x23) || _random.Calls - calls != 3,
                "Destination ENEMY$17:$02 must choose its first target and decrement $24 on its first eligible post-scroll update.");
        }
        GD.Print("Validated clean-US target Ghini state0/text eligibility, target RNG, exact acceleration/deceleration, fixed-point motion, animation, recoil and stun through gameplay updates.");
    }
}
