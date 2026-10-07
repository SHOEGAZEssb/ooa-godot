using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVireProjectileRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (int health in new[] { 20, 15, 9 })
        foreach (int subid in Enumerable.Range(0, 4))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x12); _entities.Clear();
            Vector2 floor = (from y in Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
                from x in Enumerable.Range(1, _currentRoom.WidthInTiles - 2)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_collision.Collides(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None
                select point).First();
            _player.WarpTo(floor);
            typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 100000f);
            // This bounded PART regression declares relatedObj1's live health;
            // it does not claim to execute the Vire encounter controller.
            var parent = new ArrowMoblinCharacter();
            parent.Initialize(new EnemyDatabase().ImportedEnemy(0x0c, 0), _currentRoom, floor, _random);
            parent.Health = health;
            var parentOwner = new EnemyAiSlotReservation(parent);
            _entities.AddEntity(parentOwner); _entities.RegisterEnemySlot(parentOwner, 0);
            var parts = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            var interactions = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_interactionSlots", flags)!.GetValue(_entities)!;
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc0a] = 0x8e; // PART$3a graphics resident; INTERAC$05 common graphics.
            rom[0xd080] = 1; rom[0xd081] = 0x75; rom[0xd084] = 1; rom[0xd0a9] = (byte)health;
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            bool text = true; _entities.TextActiveSource = () => text;
            int update = 0, peak = 0;
            var reservations = new List<(EnemyAiSlotReservation Owner, int Slot)>();
            var puffReservations = new List<(EnemyAiSlotReservation Owner, int Slot)>();
            void Compare()
            {
                foreach (var pair in puffReservations)
                {
                    int p = 0xd040 + pair.Slot * 256;
                    rom[p] = 1; rom[p + 1] = 0x1d; rom[p + 4] = 1;
                }
                rom[0xcba0] = (byte)(text ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                var current = parts.Where(pair => pair.Key.Node is VireProjectile).ToArray();
                peak = System.Math.Max(peak, current.Length);
                int nativeCount = Enumerable.Range(0, 16).Count(slot => rom[0xd0c0 + slot * 256] != 0 && rom[0xd0c1 + slot * 256] == 0x3a);
                FailIf(current.Length != nativeCount, $"PART$3a sub${subid:x2} HP${health:x2} update{update}: allocation/lifetime {current.Length}/{nativeCount}.");
                foreach (var pair in current)
                {
                    var actor = (VireProjectile)pair.Key.Node;
                    int p = 0xd0c0 + pair.Value * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(actor.Animation)!;
                    var xy = new Vector2(rom.Word(p + 12) / 256f, rom.Word(p + 10) / 256f);
                    FailIf(actor.SubId != rom[p + 2] || actor.Var03 != rom[p + 3] || actor.State != rom[p + 4] ||
                        actor.Angle != rom[p + 9] || actor.Speed != rom[p + 16] || actor.Position != xy ||
                        (actor.ZHigh & 0xff) != rom[p + 15] || actor.Counter1 != rom[p + 6] || actor.Counter2 != rom[p + 7] ||
                        actor.Visible != ((rom[p + 26] & 0x80) != 0) || actor.Palette != rom[p + 27] ||
                        actor.CollisionEnabled != ((rom[p + 36] & 0x80) != 0) || timer != rom[p + 32] ||
                        actor.Animation.CurrentParameter != rom[p + 33] ||
                        (actor.InvincibilityCounter & 0xff) != rom[p + 43] || actor.ContactFlags != rom[p + 42] ||
                        actor.SubId == 2 && actor.Var03 == 0 && actor.State >= 1 && actor.Target != new Vector2(rom[p + 49], rom[p + 48]) ||
                        rom.Word(p + 22) != 0xd080 || actor.PuffSlot >= 0 && rom.Word(p + 24) != 0xd040 + actor.PuffSlot * 256,
                        $"PART$3a sub${subid:x2} HP${health:x2} slot${pair.Value:x2} update{update} batch={batched}: " +
                        $"runtime={actor.State}/{actor.Speed}/{actor.Angle}/{actor.Position}, count={actor.Counter1}/{actor.Counter2}, anim={actor.Animation.AnimationIndex}/{timer}, visible={actor.Visible}; " +
                        $"ROM={rom[p + 4]}/{rom[p + 16]}/{rom[p + 9]}/{xy}, count={rom[p + 6]}/{rom[p + 7]}, anim={rom[p + 32]}, visible=${rom[p + 26]:x2}.");
                }
                var nativePuffs = Enumerable.Range(2, 14).Where(slot => rom[0xd040 + slot * 256] != 0 && rom[0xd041 + slot * 256] == 5).ToArray();
                var puffs = interactions.Where(pair => pair.Key.Node is PuzzlePuffEffect).ToArray();
                FailIf(puffs.Length != nativePuffs.Length, "PART$3a must use the real INTERAC$05 pool and deletion boundary.");
                foreach (var pair in puffs)
                {
                    var puff = (PuzzlePuffEffect)pair.Key.Node;
                    int p = 0xd040 + pair.Value * 256;
                    FailIf(puff.Position != new Vector2(rom[p + 13], rom[p + 11]) || puff.CurrentParameter != rom[p + 33] ||
                        puff.Visible != ((rom[p + 26] & 0x80) != 0), "PART$3a related puff animation/position/visibility differs.");
                }
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                    "PART$3a movement, splitting and puffs must not consume RNG.");
                update++;
            }
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            VireProjectile Spawn(Vector2 position)
            {
                var actor = _entities.Spawn<VireProjectile>(new VireProjectileSpawn(position, subid, 0, ZHigh: -4));
                int slot = parts.Single(pair => ReferenceEquals(pair.Key.Node, actor)).Value;
                int p = 0xd0c0 + slot * 256;
                rom[p] = 1; rom[p + 1] = 0x3a; rom[p + 2] = (byte)subid;
                rom[p + 11] = (byte)position.Y; rom[p + 13] = (byte)position.X; rom[p + 15] = 0xfc;
                rom.Word(p + 22, 0xd080);
                return actor;
            }
            void Release(List<(EnemyAiSlotReservation Owner, int Slot)> list, int baseAddress)
            {
                var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities", flags)!.GetValue(_entities)!;
                foreach (var pair in list)
                {
                    active.Remove(pair.Owner); typeof(RoomEntityManager).GetMethod("FreeEntity", flags)!.Invoke(_entities, [pair.Owner]);
                    for (int i = 0; i < 64; i++) rom[baseAddress + pair.Slot * 256 + i] = 0;
                }
                list.Clear();
            }
            var original = Spawn(floor + new Vector2(-16, 4));
            Step(3); // Source state0 runs during text, initialized parts freeze.
            FailIf(original.State != 1 || original.Counter1 != (subid == 3 ? 240 : 0), "PART$3a initialization/text admission differs.");
            text = false;
            if (subid == 2 && health == 20 && batched)
            {
                // Arrival must retry a checked puff allocation, then a checked
                // five-part reservation. Neither retry moves or animates.
                for (int slot = 2; slot < 16; slot++)
                {
                    var owner = new EnemyAiSlotReservation(); _entities.AddEntity(owner); interactions.Add(owner, slot);
                    puffReservations.Add((owner, slot));
                }
                int guard = 0;
                while (original.Position.Floor().DistanceTo(floor) > 2 && guard++ < 60) Step();
                Step(4); FailIf(original.State != 1, "PART$3a splitting arrival must wait while INTERACTION pool is full.");
                Release(puffReservations, 0xd040);
                while (original.State != 2 && guard++ < 100) Step();
                // Keep the final native slot occupied: the source capacity
                // helper otherwise scans beyond $df when it finds too few
                // free slots and the last one is free.
                for (int slot = 4; slot < 16; slot++)
                {
                    var owner = new EnemyAiSlotReservation(); _entities.AddEntity(owner); parts.Add(owner, slot);
                    reservations.Add((owner, slot)); int p = 0xd0c0 + slot * 256;
                    rom[p] = 1; rom[p + 1] = 0x0a; rom[p + 4] = 1; rom[p + 0x29] = 1;
                }
                Step(24); FailIf(original.State != 2 || peak != 1, "PART$3a must wait while fewer than five PART slots are free, including after the puff deletes.");
                parent.Health = 9; rom[0xd0a9] = 9; // relatedObj1 is read again for burst speed.
                Release(reservations, 0xd0c0);
                Step(); FailIf(original.State != 2, "PART$3a must observe the deleted puff's cleared parameter after missing its terminal update.");
                int reused = _entities.TryCreateTransformationPuff(original.Position.Floor());
                FailIf(reused != original.PuffSlot, "PART$3a live related pointer fixture must reuse the deleted puff's physical page.");
                int puffAddress = 0xd040 + reused * 256;
                rom[puffAddress] = 1; rom[puffAddress + 1] = 5; rom[puffAddress + 2] = 2;
                rom[puffAddress + 11] = (byte)original.Position.Y; rom[puffAddress + 13] = (byte)original.Position.X;
                guard = 0;
                while (original.State != 3 && guard++ < 32) Step();
                FailIf(original.State != 3 || original.Speed != 120 || peak != 6,
                    "PART$3a burst must resample live parent health and allocate six ordered rays.");
            }
            int limit = subid == 3 ? 250 : 520; // slow double rays can cross the large room.
            int remaining = 0;
            while (parts.Keys.Any(entity => entity.Node is VireProjectile) && remaining++ < limit) Step();
            Step(20); // finish any collision/expiry puffs before slot reuse.
            FailIf(parts.Keys.Any(entity => entity.Node is VireProjectile) || peak != (subid == 1 ? 2 : subid == 2 ? 6 : 1),
                $"PART$3a sub${subid:x2} HP${health:x2} batch={batched} must execute its complete source flight/split/expiry lifetime: peak={peak}, remaining={parts.Keys.Count(entity => entity.Node is VireProjectile)}.");
            // Repopulate the freed slot and publish an overlapping sword hit.
            // Its pending signal survives text, then deletes with one puff.
            var repeated = Spawn(floor + new Vector2(-16, 4)); Step();
            var target = parts.Single(pair => ReferenceEquals(pair.Key.Node, repeated));
            ((ISwordHittableRoomEntity)target.Key).ApplySwordHit(repeated.CollisionBounds, floor, 1,
                EnemyKnockbackStrength.Low, new List<RoomEntitySpawn>());
            rom.HitPartWithItem(4, target.Value);
            text = true; Step(3); FailIf(repeated.Finished || !repeated.PendingCollision, "PART$3a pending item contact must survive text.");
            text = false; Step(); FailIf(!repeated.Finished, "PART$3a sword contact must create a puff and delete on its next eligible handler.");
            Step(24);
        }
        GD.Print("Validated clean-US PART$3a four forms and three health speed tiers through individual/batched gameplay object walks: animation fallthrough, fixed motion, live parent reads, ordered double/six-way allocations, checked-pool retries, homing cadence/expiry and pending sword contact across text with slot reuse.");
    }
}
